using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ashare.V3.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Data.Templates;

/// <summary>
/// يَبني <see cref="AttributeTemplate"/> لِفِئَة مُعَيَّنَة بِالقِراءَة مِن
/// جَداوِل asharedb المُنَظَّمَة (<c>CategoryAttributeMappings +
/// AttributeDefinitions + AttributeValues</c>). هذا هو المَصدَر الكانوني
/// عِندَ تَوَفُّر بَيانات إنتاج. لَو الجَداوِل فارِغَة (dev بِلا clone)،
/// يُعيد <c>null</c> ⇒ المُستَهلِك يَتَدَرَّج لِـ <c>CategoryAttributeTemplates</c>
/// (DB-served code seed) ثُمّ <see cref="V3CategoryTemplates"/>.
///
/// <para>تَحويل الأَنواع (<c>AttributeDefinition.Type</c> → string في
/// <see cref="AttributeFieldDefinition.Type"/>):</para>
/// <list type="bullet">
///   <item>1 SingleSelect → <c>select</c></item>
///   <item>2 MultiSelect → <c>multi</c></item>
///   <item>3 Number → <c>number</c></item>
///   <item>4 Text / 5 LongText / 9 File / 10 Color → <c>text</c></item>
///   <item>6 Boolean → <c>bool</c></item>
///   <item>7 Date / 8 DateTime → <c>date</c></item>
/// </list>
/// </summary>
public sealed class ProductionAttributeTemplateSource
{
    private readonly AshareV3DbContext _db;
    public ProductionAttributeTemplateSource(AshareV3DbContext db) => _db = db;

    /// <summary>
    /// يَبني template لِفِئَة بِواسِطَة Guid. <c>null</c> ⇒ لا mappings
    /// (مَعنى الـ caller أَن يَتَدَرَّج لِمَصدَر آخَر).
    /// </summary>
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

        // اِجلِب كُلّ الـ values لِلـ defs الَّتي تَحتاجها (select/multi).
        var selectDefIds = defs.Values
            .Where(d => d.Type == 1 || d.Type == 2)
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
                Label    = d.Name,                 // الإنتاج يَحفَظ name واحِد فَقَط (عَرَبي)
                LabelAr  = d.Name,                 // نُسَجِّله في كِلا الحَقلَين لِتَتَوافَق
                Type     = MapType(d.Type),
                Required = m.IsRequiredOverride ?? d.IsRequired,
                ShowInCard = d.IsVisibleInList,
                SortOrder  = m.SortOrder != 0 ? m.SortOrder : ++orderBase,
                Default    = d.Default(),
                Options    = MapOptions(d.Type, valuesByDef.GetValueOrDefault(d.Id)),
            });
        }
        return new AttributeTemplate { Fields = fields };
    }

    private static string MapType(int t) => t switch
    {
        1 => "select",
        2 => "multi",
        3 => "number",
        6 => "bool",
        7 or 8 => "date",
        _ => "text",
    };

    private static List<AttributeOption> MapOptions(int type, List<AttributeValueEntity>? values)
    {
        if (values is null || (type != 1 && type != 2)) return new();
        return values.Select(v => new AttributeOption
        {
            Value   = v.Value,
            Label   = v.DisplayName ?? v.Value,
            LabelAr = v.DisplayName ?? v.Value,
        }).ToList();
    }
}

internal static class AttributeDefinitionExtensions
{
    public static object? Default(this AttributeDefinitionEntity d) =>
        string.IsNullOrEmpty(d.DefaultValue) ? null : d.DefaultValue;
}
