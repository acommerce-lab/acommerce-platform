using ACommerce.Kits.DynamicAttributes.Backend;
using ACommerce.SharedKernel.Domain.DynamicAttributes;

namespace Ejar.Api.Data.Templates;

/// <summary>
/// مَصدَر قَوالِب Ejar — يَبني <see cref="AttributeTemplate"/> في الذاكِرَة
/// مَن <see cref="EjarProfileAttributes.Defaults"/> أَو
/// <see cref="EjarListingAttributes.Defaults"/> بِناءً عَلى الـ scopeId.
///
/// <para>Ejar لا يَملِك جَداوِل <c>AttributeDefinitions</c> (كَما في
/// asharedb لِـ Ashare V3)؛ القَوالِب مُعَرَّفَة كود ثابِت — لِتَوسيعها
/// عَدِّل المَلَفّ <c>EjarAttributes.cs</c> ثُمّ أَعِد التَّشغيل.</para>
///
/// <para><b>Reserved keys</b>: لا داعي لِفِلتَر هُنا — Defaults مَكتوبَة
/// يَدَوِيّاً، فَلا تَكرار مَع حُقول الواجِهَة.</para>
/// </summary>
public sealed class EjarAttributeTemplateSource : IAttributeTemplateSource
{
    public Task<AttributeTemplate?> BuildForScopeAsync(Guid scopeId, CancellationToken ct)
    {
        if (scopeId == EjarProfileAttributes.ScopeId)
            return Task.FromResult<AttributeTemplate?>(BuildFromSeeds(EjarProfileAttributes.Defaults));

        // أَيّ scopeId آخَر = نَوع عَقار مُشتَقّ مَن PropertyType slug.
        // كُلّ الفِئات تَستَلِم نَفس الـ Listing defaults الآن — يُمكِن
        // تَخصيص لاحِقاً (per-property-type defaults).
        return Task.FromResult<AttributeTemplate?>(BuildFromSeeds(EjarListingAttributes.Defaults));
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
