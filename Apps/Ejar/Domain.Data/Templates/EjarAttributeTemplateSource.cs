using ACommerce.Kits.DynamicAttributes.Backend;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ejar.Api.Data;
using Ejar.Domain;

namespace Ejar.Api.Data.Templates;

/// <summary>
/// مَصدَر قَوالِب Ejar — يَبني <see cref="AttributeTemplate"/> في الذاكِرَة.
/// يُمَيِّز ثَلاث حالات:
///
/// <list type="number">
///   <item><b>Profile scope</b>: قالَب Bio/Occupation/Nationality/Languages.</item>
///   <item><b>Listing scope</b>: scopeId مُشتَقّ مَن PropertyType slug.
///         نَعكِس الـ slug ⇒ نَستَخرِج kind مَن EjarSeed.Categories ⇒
///         نَعيد القالَب المُناسِب (Realty/Vehicle/Event/Camps).</item>
///   <item><b>Unknown</b>: قالَب universal فارِغ.</item>
/// </list>
///
/// <para><b>لِماذا per-kind</b>: حَلّ مَشكَلَة "إعلان باص يَطلُب
/// عَدَد غُرَف". القالَب يَختَلِف حَسب الـ kind، لا يَوجَد قالَب واحِد.</para>
///
/// <para>الـ scopeId reverse-lookup: نَبني <see cref="_scopeToKind"/>
/// مَرَّة واحِدَة عِندَ static ctor — يَحوي Guid لِكُلّ slug مَعروف.</para>
/// </summary>
public sealed class EjarAttributeTemplateSource : IAttributeTemplateSource
{
    /// <summary>خَريطَة <c>scopeId (مُشتَقّ مَن slug) → kind</c>. تُبنى
    /// مَرَّة عِندَ تَحميل الصَنف.</summary>
    private static readonly Dictionary<Guid, string> _scopeToKind = BuildScopeMap();

    private static Dictionary<Guid, string> BuildScopeMap()
    {
        var map = new Dictionary<Guid, string>();
        foreach (var c in EjarSeed.Categories)
            map[EjarListingScopes.DeriveScopeId(c.Id)] = c.Kind;
        return map;
    }

    public Task<AttributeTemplate?> BuildForScopeAsync(Guid scopeId, CancellationToken ct)
    {
        if (scopeId == EjarProfileAttributes.ScopeId)
            return Task.FromResult<AttributeTemplate?>(BuildFromSeeds(EjarProfileAttributes.Defaults));

        // Listing scope: نَكتَشِف الـ kind مَن scopeId ⇒ نَعيد القالَب المُلائِم.
        if (_scopeToKind.TryGetValue(scopeId, out var kind))
        {
            var seeds = EjarListingAttributes.ForKind(kind);
            return Task.FromResult<AttributeTemplate?>(BuildFromSeeds(seeds));
        }

        // غَير مَعروف ⇒ universal (فارِغَة الآن — لا تَكرار مَع IListing).
        return Task.FromResult<AttributeTemplate?>(BuildFromSeeds(EjarListingAttributes.UniversalDefaults));
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

    /// <summary>قاموس تَرجَمات لِخِيارات Ejar الافتِراضِيَّة.</summary>
    private static string TranslateOption(string defCode, string value) => (defCode, value) switch
    {
        ("Floor", "ground")        => "أَرضي",
        ("Floor", "first")         => "الأَوَّل",
        ("Floor", "second")        => "الثاني",
        ("Floor", "third")         => "الثالِث",
        ("Floor", "fourth")        => "الرابِع",
        ("Floor", "fifth")         => "الخامِس",
        ("Furnished", "furnished")     => "مُؤَثَّث",
        ("Furnished", "unfurnished")   => "غَير مُؤَثَّث",
        ("Furnished", "semi")          => "نِصف مُؤَثَّث",
        ("Parking", "yes")         => "مُتَوَفِّر",
        ("Parking", "no")          => "غَير مُتَوَفِّر",
        ("Parking", "covered")     => "مُغَطّى",
        _                          => value,
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
