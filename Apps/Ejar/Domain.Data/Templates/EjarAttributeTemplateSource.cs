using ACommerce.Kits.DynamicAttributes.Backend;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ejar.Api.Data;
using Ejar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Data.Templates;

/// <summary>
/// مَصدَر قَوالِب سِمات إيجار. النَّمَط نَفسه الَّذي يَستَخدِمه V3:
///
/// <list type="number">
///   <item><b>DB أَوَّلاً</b>: نَقرَأ <c>CategoryAttributeMappings</c> +
///         <c>AttributeDefinitions</c> + <c>AttributeValues</c> لِبِناء
///         <see cref="AttributeTemplate"/>. هذا هو الـ source الكانوني
///         بَعد ركض الـ migration + الـ seed
///         (<see cref="DbInitializer.SeedListingAttributesIfMissing"/>).</item>
///   <item><b>Fallback in-memory</b>: لَو DB فارِغَة (أَوَّل تَشغيل قَبل
///         الـ migration، أَو فَشِل الـ seed) نَبني الـ template مَن
///         <see cref="EjarListingAttributes"/> / <see cref="EjarProfileAttributes"/>
///         مُباشَرَةً — لِيَعمَل الـ wizard فَوراً بِلا تَدَخُّل.</item>
/// </list>
///
/// <para><b>وَعد التَحَكُّم</b>: بِما أَنّ DB هو الـ source الكانوني، لوحَة
/// الإدارَة المُستَقبَلِيَّة تَستَطيع إضافَة/تَعديل/حَذف سِمَة لِفِئَة دون
/// نَشر كود — تُعَدِّل صُفوف <c>AttributeDefinitions</c> /
/// <c>CategoryAttributeMappings</c> مُباشَرَةً. الـ in-memory يَبقى كَ
/// <i>seed</i> لِبِدايَة سَريعَة.</para>
/// </summary>
public sealed class EjarAttributeTemplateSource : IAttributeTemplateSource
{
    private readonly EjarDbContext _db;
    public EjarAttributeTemplateSource(EjarDbContext db) => _db = db;

    /// <summary>خَريطَة <c>scopeId (مُشتَقّ مَن slug) → kind</c>. تُبنى
    /// مَرَّة عِندَ تَحميل الصَنف. تَخدِم fallback in-memory فَقَط.</summary>
    private static readonly Dictionary<Guid, string> _scopeToKind = BuildScopeMap();

    private static Dictionary<Guid, string> BuildScopeMap()
    {
        var map = new Dictionary<Guid, string>();
        foreach (var c in EjarSeed.Categories)
            map[EjarListingScopes.DeriveScopeId(c.Id)] = c.Kind;
        return map;
    }

    public async Task<AttributeTemplate?> BuildForScopeAsync(Guid scopeId, CancellationToken ct)
    {
        // ① مُحاوَلَة DB. لَو فيها mappings ⇒ هي الـ truth.
        try
        {
            var fromDb = await BuildFromDbAsync(scopeId, ct);
            if (fromDb is { Fields.Count: > 0 }) return fromDb;
        }
        catch
        {
            // DB قَد لا تَكون جاهِزَة (الـ migration لَم يَركَض، أَو الجَدول
            // مَفقود). نُكَمِّل بِالـ in-memory fallback.
        }

        // ② Fallback in-memory.
        if (scopeId == EjarProfileAttributes.ScopeId)
            return BuildFromSeeds(EjarProfileAttributes.Defaults);

        if (_scopeToKind.TryGetValue(scopeId, out var kind))
            return BuildFromSeeds(EjarListingAttributes.ForKind(kind));

        return BuildFromSeeds(EjarListingAttributes.UniversalDefaults);
    }

    private async Task<AttributeTemplate?> BuildFromDbAsync(Guid scopeId, CancellationToken ct)
    {
        var mappings = await _db.CategoryAttributeMappings.AsNoTracking()
            .Where(m => m.CategoryId == scopeId && m.IsActive)
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

        var fields = new List<AttributeFieldDefinition>(mappings.Count);
        var orderBase = 0;
        foreach (var m in mappings)
        {
            if (!defs.TryGetValue(m.AttributeDefinitionId, out var d)) continue;
            fields.Add(new AttributeFieldDefinition
            {
                Key      = d.Code,
                Label    = d.Name,
                LabelAr  = d.Name,
                Type     = MapType(d.Type),
                Required = m.IsRequiredOverride ?? d.IsRequired,
                ShowInCard = d.IsVisibleInList,
                SortOrder = m.SortOrder != 0 ? m.SortOrder : ++orderBase,
                Default   = string.IsNullOrEmpty(d.DefaultValue) ? null : d.DefaultValue,
                Options   = MapOptionsFromDb(d.Code, d.Type, valuesByDef.GetValueOrDefault(d.Id)),
            });
        }
        return new AttributeTemplate { Fields = fields };
    }

    private static bool IsSelectLike(string t) =>
        string.Equals(t, "SingleSelect", StringComparison.OrdinalIgnoreCase)
        || string.Equals(t, "MultiSelect", StringComparison.OrdinalIgnoreCase);

    private static List<AttributeOption> MapOptionsFromDb(
        string defCode, string type, List<AttributeValueEntity>? values)
    {
        if (values is null || !IsSelectLike(type)) return new();
        return values.Select(v => new AttributeOption
        {
            Value   = v.Value,
            Label   = v.DisplayName ?? v.Value,
            LabelAr = TranslateOption(defCode, v.Value),
        }).ToList();
    }

    private static AttributeTemplate BuildFromSeeds(IReadOnlyList<AttributeSeed> seeds)
    {
        var sort = 0;
        var fields = seeds.Select(s => new AttributeFieldDefinition
        {
            Key      = s.Code,
            Label    = s.Name,
            LabelAr  = s.Name,
            Type     = MapType(s.Type),
            SortOrder = ++sort,
            Options  = (s.Options ?? Array.Empty<string>())
                        .Select(v => new AttributeOption
                        {
                            Value   = v,
                            Label   = v,
                            LabelAr = TranslateOption(s.Code, v),
                        })
                        .ToList(),
        }).ToList();
        return new AttributeTemplate { Fields = fields };
    }

    /// <summary>قاموس تَرجَمات لِخِيارات Ejar الافتِراضِيَّة.
    /// يَختار التَّرجَمَة المُحَدَّدَة لِلحَقل ثُمّ يَرجِع إلى قاموس
    /// <see cref="_genericOptions"/> العام (لِقِيَم تَتَكَرَّر عَبر حُقول
    /// كَ "yes/no", "indoor/outdoor", "monthly/yearly").</summary>
    private static string TranslateOption(string defCode, string value)
    {
        var specific = (defCode, value) switch
        {
            ("Floor", "ground")        => "أَرضي",
            ("Floor", "first")         => "الأَوَّل",
            ("Floor", "second")        => "الثاني",
            ("Floor", "third")         => "الثالِث",
            ("Floor", "fourth")        => "الرابِع",
            ("Floor", "fifth")         => "الخامِس",
            ("Floor", "sixth")         => "السادِس",
            ("Floor", "seventh")       => "السابِع",
            ("Floor", "eighth")        => "الثامِن",
            ("Floor", "ninth")         => "التاسِع",
            ("Floor", "tenth")         => "العاشِر",
            ("Furnished", "furnished")    => "مُؤَثَّث",
            ("Furnished", "unfurnished")  => "غَير مُؤَثَّث",
            ("Furnished", "semi")         => "نِصف مُؤَثَّث",
            ("Parking", "yes")            => "مُتَوَفِّر",
            ("Parking", "no")             => "غَير مُتَوَفِّر",
            ("Parking", "covered")        => "مُغَطّى",
            ("Parking", "street")         => "في الشارِع",
            _                             => null,
        };
        if (specific is not null) return specific;
        return _genericOptions.TryGetValue(value, out var v) ? v : value;
    }

    private static readonly Dictionary<string, string> _genericOptions = new()
    {
        // Yes/No
        ["yes"] = "نَعَم", ["no"] = "لا",

        // Lease term / payment schedules
        ["daily"] = "يَومي", ["weekly"] = "أُسبوعي", ["monthly"] = "شَهري",
        ["quarterly"] = "رُبع سَنَوي", ["semi_annual"] = "نِصف سَنَوي",
        ["annual"] = "سَنَوي", ["yearly"] = "سَنَوي", ["upfront"] = "دَفعَة واحِدَة",

        // Indoor/Outdoor
        ["indoor"] = "داخِلي", ["outdoor"] = "خارِجي", ["both"] = "كِلاهُما",

        // Utilities included
        ["all"] = "الكُلّ", ["electricity"] = "كَهرَباء",
        ["water"] = "ماء", ["internet"] = "إنتَرنِت", ["none"] = "لا شَيء",

        // Water source
        ["public"] = "شَبَكَة عامَّة", ["well"] = "بِئر", ["tanker"] = "وايِت",

        // Power backup
        ["generator"] = "مُولِّد", ["solar"] = "طاقَة شَمسِيَّة",
        ["battery"] = "بَطّارِيَّات", ["ups"] = "UPS",

        // View
        ["sea"] = "إطلالَة بَحرِيَّة", ["mountain"] = "إطلالَة جَبَلِيَّة",
        ["city"] = "إطلالَة عَلى المَدينَة", ["garden"] = "إطلالَة عَلى الحَديقَة",
        ["street"] = "إطلالَة عَلى الشارِع",

        // Orientation
        ["north"] = "شَمال", ["south"] = "جَنوب", ["east"] = "شَرق", ["west"] = "غَرب",
        ["northeast"] = "شَمال شَرقي", ["northwest"] = "شَمال غَربي",
        ["southeast"] = "جَنوب شَرقي", ["southwest"] = "جَنوب غَربي",

        // Condition
        ["new"] = "جَديد", ["excellent"] = "مُمتاز",
        ["good"] = "جَيِّد", ["fair"] = "مَقبول",
        ["needs_renovation"] = "يَحتاج صِيانَة",

        // Building type
        ["tower"] = "بُرج", ["house"] = "بَيت مُستَقِلّ", ["compound"] = "مُجَمَّع",
        ["commercial_building"] = "مَبنى تِجاري", ["mall"] = "مول",

        // Policies
        ["allowed"] = "مَسموح", ["not_allowed"] = "غَير مَسموح",
        ["case_by_case"] = "حَسب الحالَة", ["outdoor_only"] = "في الخارِج فَقَط",

        // Vehicle bodies
        ["sedan"] = "سيدان", ["suv"] = "SUV", ["hatchback"] = "هاتشباك",
        ["coupe"] = "كوبيه", ["pickup"] = "بِك أَب", ["van"] = "فان",
        ["bus"] = "باص", ["minibus"] = "باص صَغير",
        ["motorcycle"] = "درّاجَة ناريَّة", ["scooter"] = "سكوتَر",

        // Fuel
        ["petrol"] = "بَنزين", ["diesel"] = "ديزِل",
        ["electric"] = "كَهرَبائي", ["hybrid"] = "هايبرِد", ["gas"] = "غاز",

        // Transmission
        ["automatic"] = "أوتوماتيك", ["manual"] = "عادي",
        ["cvt"] = "CVT", ["dual_clutch"] = "دَبَل كلاتش",

        // Drive
        ["fwd"] = "دَفع أَمامي", ["rwd"] = "دَفع خَلفي",
        ["awd"] = "دَفع رُباعي دائِم", ["4wd"] = "دَفع رُباعي",

        // License
        ["light"] = "خَفيفَة", ["heavy"] = "ثَقيلَة",
        ["public_transport"] = "نَقل عامّ",

        // Event types
        ["wedding"] = "زَواج", ["engagement"] = "خِطبَة",
        ["graduation"] = "تَخَرُّج", ["birthday"] = "عيد ميلاد",
        ["corporate"] = "شَرِكات", ["conference"] = "مُؤتَمَر",
        ["funeral"] = "عَزاء", ["other"] = "أُخرى",

        // Slots
        ["morning"] = "صَباحي", ["evening"] = "مَسائي",

        // Cancel policy
        ["flexible"] = "مَرِنَة", ["moderate"] = "مُتَوَسِّطَة",
        ["strict"] = "صارِمَة", ["non_refundable"] = "غَير قابِلَة لِلاستِرداد",

        // Camp style / type
        ["modern"] = "حَديث", ["traditional"] = "تُراثي",
        ["desert"] = "صَحراوي", ["beach"] = "ساحِلي",
        ["weddings"] = "أَفراح", ["family"] = "عائِلي",
        ["tourism"] = "سِياحَة", ["mixed"] = "مُتَعَدِّد",
    };

    private static string MapType(string t) => t switch
    {
        "SingleSelect"             => "select",
        "MultiSelect"              => "multi",
        "Number"                   => "number",
        "Boolean"                  => "bool",
        "Date" or "DateTime"       => "date",
        _                          => "text",
    };
}
