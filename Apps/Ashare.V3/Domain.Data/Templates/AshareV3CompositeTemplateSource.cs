using ACommerce.Kits.DynamicAttributes.Backend;
using ACommerce.SharedKernel.Domain.DynamicAttributes;

namespace Ashare.V3.Data.Templates;

/// <summary>
/// مَصدَر قَوالِب مُرَكَّب لِـ V3:
/// <list type="number">
///   <item>أَيّ scope ⇒ <see cref="ProductionAttributeTemplateSource"/> أَوَّلاً
///         (يَقرَأ مَن جَداوِل <c>CategoryAttributeMappings</c> +
///         <c>AttributeDefinitions</c> + <c>AttributeValues</c>).</item>
///   <item>لَو الـ prod source رَدّ <c>null</c>/فارِغ لِسلاجات الـ roommate،
///         نَرُدّ القالَب in-memory مَن <see cref="AshareV3RoommateAttributes"/>.
///         الـ in-memory هو fallback آمِن لَو الـ seed لَم يَركَض بَعد.</item>
/// </list>
///
/// <para>بَعد ركض <see cref="Bootstrap.AshareV3Bootstrap"/> الـ DB سَتَكون
/// فيها التَّعريفات والـ mappings ⇒ الـ prod source يَكفي وَحدَه، فَلا
/// نَصِل أَبَداً إلى الـ in-memory fallback. الـ class يَبقى لِسَلامَة الإقلاع
/// الأَوَّل ولِحالات seed-failure.</para>
/// </summary>
public sealed class AshareV3CompositeTemplateSource : IAttributeTemplateSource
{
    private readonly ProductionAttributeTemplateSource _prod;
    public AshareV3CompositeTemplateSource(ProductionAttributeTemplateSource prod) => _prod = prod;

    public async Task<AttributeTemplate?> BuildForScopeAsync(Guid scopeId, CancellationToken ct)
    {
        // ① الـ DB أَوَّلاً — يَنطَبِق عَلى الكُلّ (روممَت بَعد seed، فِئات الإنتاج
        // كَما هي). الـ ProductionAttributeTemplateSource يَرُدّ null لَو ما
        // فيش mappings.
        var fromDb = await _prod.BuildForScopeAsync(scopeId, ct);
        if (fromDb is { Fields.Count: > 0 })
            return fromDb;

        // ② Fallback in-memory لِلروممَت — لَو الـ seed لَم يَركَض.
        if (scopeId == AshareV3RoommateAttributes.RoommateHasCategoryId)
            return BuildTemplate(AshareV3RoommateAttributes.RoommateHasFields);
        if (scopeId == AshareV3RoommateAttributes.RoommateWantsCategoryId)
            return BuildTemplate(AshareV3RoommateAttributes.RoommateWantsFields);

        return fromDb;   // غَير روممَت + لا في DB ⇒ null/فارِغ
    }

    /// <summary>يُحَوِّل قائِمَة <see cref="AshareV3RoommateAttributes.AttrSeed"/>
    /// إلى <see cref="AttributeTemplate"/> جاهِز لِلواجِهَة. نَفس التَّحويل
    /// المَنطِقي الَّذي يَفعَله <see cref="ProductionAttributeTemplateSource"/>
    /// عِندَ قِراءَة الـ DB rows.</summary>
    private static AttributeTemplate BuildTemplate(IReadOnlyList<AshareV3RoommateAttributes.AttrSeed> seeds)
    {
        var fields = new List<AttributeFieldDefinition>(seeds.Count);
        var sort = 0;
        foreach (var s in seeds)
        {
            sort++;
            fields.Add(new AttributeFieldDefinition
            {
                Key       = s.Code,
                Label     = s.Name,
                LabelAr   = s.Name,
                Type      = MapType(s.Type),
                SortOrder = sort,
                Options   = (s.Options ?? Array.Empty<AshareV3RoommateAttributes.OptSeed>())
                    .Select(o => new AttributeOption
                    {
                        Value   = o.Value,
                        Label   = o.Value,
                        LabelAr = o.LabelAr,
                    }).ToList(),
            });
        }
        return new AttributeTemplate { Fields = fields };
    }

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
