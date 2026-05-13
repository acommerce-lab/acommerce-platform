using ACommerce.Kits.DynamicAttributes.Backend;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ashare.V3.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Data.Templates;

/// <summary>
/// يَبني <see cref="AttributeTemplate"/> لِفِئَة مُعَيَّنَة بِالقِراءَة مِن
/// جَداوِل asharedb المُنَظَّمَة (<c>CategoryAttributeMappings +
/// AttributeDefinitions + AttributeValues</c>).
///
/// <para>تَحويل أَنواع <c>AttributeDefinition.Type</c> (string في
/// asharedb بِـ <c>HasConversion&lt;string&gt;</c> عَلى enum AttributeType):</para>
/// <list type="bullet">
///   <item>SingleSelect → <c>select</c></item>
///   <item>MultiSelect → <c>multi</c></item>
///   <item>Number → <c>number</c></item>
///   <item>Text / LongText / File / Color → <c>text</c></item>
///   <item>Boolean → <c>bool</c></item>
///   <item>Date / DateTime → <c>date</c></item>
/// </list>
/// </summary>
public sealed class ProductionAttributeTemplateSource : IAttributeTemplateSource
{
    private readonly AshareV3DbContext _db;
    public ProductionAttributeTemplateSource(AshareV3DbContext db) => _db = db;

    /// <summary>
    /// مَفاتيح مَحجوزَة لِنِطاق <c>ProductListing</c> — كُلّ ما يُغَطّيه
    /// حَقل ثابِت في <c>IListing</c>/<c>ProductListingEntity</c>. القيَم
    /// مُخَزَّنَة بِشَكل <b>مُطَبَّع</b> (lowercase + بِلا underscores/hyphens)
    /// لِنَلتَقِط <c>time_unit</c>/<c>TimeUnit</c>/<c>timeUnit</c> دَفعَة واحِدَة
    /// بِدون تَعداد كُلّ الـ aliases. التَطبيع تَمّ في <see cref="Normalize"/>.
    ///
    /// <para>القائِمَة تَستَنِد إلى نَتائِج فَحص جَدول
    /// <c>AttributeDefinitions</c> الإنتاجي + <c>AttributesJson</c> الفِعلِيَّة
    /// عَلى الإعلانات: aliases مَوجودَة فِعليّاً ⇒ <c>rooms</c> (= BedroomCount)،
    /// <c>location</c> (= District)، <c>features</c> (= Amenities).</para>
    /// </summary>
    private static readonly HashSet<string> ListingReservedKeys = new(StringComparer.Ordinal)
    {
        // IListing core
        "title", "description", "price", "timeunit",
        "city", "district", "address", "location",
        "lat", "lng", "latitude", "longitude",
        "bedroomcount", "bedrooms", "bedroom", "rooms",
        "bathroomcount", "bathrooms", "bathroom",
        "areasqm", "area", "size",
        "amenities", "amenity", "features",
        "images", "image", "imagesjson", "thumbnail", "featuredimage",
        "status", "viewscount", "viewcount",
        "isverified", "isfeatured", "isactive",
        "propertytype", "condition",
        "categoryid", "vendorid", "ownerid",
        "createdat", "updatedat",
    };

    /// <summary>مَفاتيح مَحجوزَة لِـ Profile (مُطَبَّعَة) — حُقول
    /// <c>IUserProfile</c> + الأَعمِدَة السَطحِيَّة الَّتي لا تُحَوَّل
    /// لِديناميكي (UserId/NationalId — هَويَّة + Nafath lookup).</summary>
    private static readonly HashSet<string> ProfileReservedKeys = new(StringComparer.Ordinal)
    {
        "id", "userid", "fullname",
        "phone", "phonenumber", "phoneverified",
        "email", "emailverified", "city", "avatarurl", "avatar",
        "nationalid",
        "createdat", "updatedat", "membersince",
    };

    /// <summary>تَطبيع code المُقارَنَة: lowercase + إزالة كُلّ غَير
    /// alphanumeric. <c>"time_unit"</c>, <c>"TimeUnit"</c>, <c>"timeUnit"</c>,
    /// <c>"time-unit"</c> كُلُّها ⇒ <c>"timeunit"</c>.</summary>
    private static string Normalize(string code)
    {
        Span<char> buf = stackalloc char[code.Length];
        int n = 0;
        foreach (var c in code)
            if (char.IsLetterOrDigit(c)) buf[n++] = char.ToLowerInvariant(c);
        return new string(buf[..n]);
    }

    private static readonly Guid ProfileScopeId = new("00000000-0000-0000-0000-000000000F01");

    /// <summary>تَنفيذ <see cref="IAttributeTemplateSource.BuildForScopeAsync"/>.
    /// في أَسهَر، الـ scopeId = إِمّا <c>ProductCategory.Id</c> لِلإعلانات،
    /// أَو sentinel ثابِت لِكِيانات أُخرى (Profile).</summary>
    public Task<AttributeTemplate?> BuildForScopeAsync(Guid scopeId, CancellationToken ct)
        => BuildForCategoryAsync(scopeId, ct);

    public async Task<AttributeTemplate?> BuildForCategoryAsync(Guid categoryId, CancellationToken ct)
    {
        var mappings = await _db.CategoryAttributeMappings.AsNoTracking()
            .Where(m => m.CategoryId == categoryId && m.IsActive)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);
        if (mappings.Count == 0) return null;

        var defIds = mappings.Select(m => m.AttributeDefinitionId).ToList();
        var defs = await _db.AttributeDefinitions.AsNoTracking()
            .Where(d => defIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);

        var selectDefIds = defs.Values
            .Where(d => IsSelectLike(d.Type))
            .Select(d => d.Id).ToList();
        var allValues = selectDefIds.Count == 0
            ? new List<AttributeValueEntity>()
            : await _db.AttributeValues.AsNoTracking()
                .Where(v => selectDefIds.Contains(v.AttributeDefinitionId) && v.IsActive)
                .OrderBy(v => v.SortOrder)
                .ToListAsync(ct);
        var valuesByDef = allValues.GroupBy(v => v.AttributeDefinitionId)
                                   .ToDictionary(g => g.Key, g => g.ToList());

        // اِختَر القائِمَة المَحجوزَة حَسب النِطاق: sentinel Profile لَه قائِمَة
        // واجِهَة IUserProfile؛ أَيّ نِطاق آخَر (CategoryId حَقيقي) لَه قائِمَة
        // واجِهَة IListing.
        var reservedKeys = categoryId == ProfileScopeId
            ? ProfileReservedKeys
            : ListingReservedKeys;

        var fields = new List<AttributeFieldDefinition>();
        var orderBase = 0;
        foreach (var m in mappings)
        {
            if (!defs.TryGetValue(m.AttributeDefinitionId, out var d)) continue;
            // اِسقاط مُبَكِّر: لَو Code (بَعد التَطبيع) يُطابِق حَقل ثابِت
            // عَلى الكِيان، لا نُدرِجه في القالَب. الـ wizard/edit page
            // يَعرِض الحَقل الثابِت بِواجِهَته المُخَصَّصَة، فَلا داعي
            // لِتَكرارَه ديناميكيّاً. التَطبيع يَلتَقِط <c>time_unit</c>،
            // <c>TimeUnit</c>، <c>timeUnit</c> ⇒ كُلُّها <c>timeunit</c>.
            var code = string.IsNullOrEmpty(d.Code) ? d.Id.ToString("N") : d.Code;
            if (reservedKeys.Contains(Normalize(code))) continue;

            fields.Add(new AttributeFieldDefinition
            {
                Key      = code,
                Label    = d.Name,
                LabelAr  = d.Name,
                Type     = MapType(d.Type),
                Required = m.IsRequiredOverride ?? d.IsRequired,
                ShowInCard = d.IsVisibleInList,
                SortOrder  = m.SortOrder != 0 ? m.SortOrder : ++orderBase,
                Default    = string.IsNullOrEmpty(d.DefaultValue) ? null : d.DefaultValue,
                Options    = MapOptions(code, d.Type, valuesByDef.GetValueOrDefault(d.Id)),
            });
        }
        return new AttributeTemplate { Fields = fields };
    }

    private static bool IsSelectLike(string t) =>
        string.Equals(t, "SingleSelect", StringComparison.OrdinalIgnoreCase)
        || string.Equals(t, "MultiSelect", StringComparison.OrdinalIgnoreCase);

    private static string MapType(string t) => t switch
    {
        "SingleSelect"             => "select",
        "MultiSelect"              => "multi",
        "Number"                   => "number",
        "Boolean"                  => "bool",
        "Date" or "DateTime"       => "date",
        _                          => "text",
    };

    private static List<AttributeOption> MapOptions(
        string defCode, string type, List<AttributeValueEntity>? values)
    {
        if (values is null || !IsSelectLike(type)) return new();
        return values.Select(v => new AttributeOption
        {
            Value   = v.Value,
            Label   = v.DisplayName ?? v.Value,
            // LabelAr: قاموس <c>V3AttributeValueTranslations</c> يُجَرِّب
            // Value أَوَّلاً ثُمّ DisplayName ⇒ يَلتَقِط حالَة Value="3rd" مَع
            // DisplayName="third"؛ fallback إلى DisplayName الإنتاجي.
            LabelAr = V3AttributeValueTranslations.TryTranslate(defCode, v.Value, v.DisplayName)
                      ?? v.DisplayName ?? v.Value,
        }).ToList();
    }
}
