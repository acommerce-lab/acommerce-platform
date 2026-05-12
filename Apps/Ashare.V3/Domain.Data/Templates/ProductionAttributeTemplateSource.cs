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

        var fields = new List<AttributeFieldDefinition>();
        var orderBase = 0;
        foreach (var m in mappings)
        {
            if (!defs.TryGetValue(m.AttributeDefinitionId, out var d)) continue;
            fields.Add(new AttributeFieldDefinition
            {
                Key      = string.IsNullOrEmpty(d.Code) ? d.Id.ToString("N") : d.Code,
                Label    = d.Name,
                LabelAr  = d.Name,
                Type     = MapType(d.Type),
                Required = m.IsRequiredOverride ?? d.IsRequired,
                ShowInCard = d.IsVisibleInList,
                SortOrder  = m.SortOrder != 0 ? m.SortOrder : ++orderBase,
                Default    = string.IsNullOrEmpty(d.DefaultValue) ? null : d.DefaultValue,
                Options    = MapOptions(d.Type, valuesByDef.GetValueOrDefault(d.Id)),
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

    private static List<AttributeOption> MapOptions(string type, List<AttributeValueEntity>? values)
    {
        if (values is null || !IsSelectLike(type)) return new();
        return values.Select(v => new AttributeOption
        {
            Value   = v.Value,
            Label   = v.DisplayName ?? v.Value,
            LabelAr = v.DisplayName ?? v.Value,
        }).ToList();
    }
}
