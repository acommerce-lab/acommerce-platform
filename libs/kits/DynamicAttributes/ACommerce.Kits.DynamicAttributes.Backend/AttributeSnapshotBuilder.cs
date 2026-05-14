using ACommerce.Kits.DynamicAttributes.Operations;
using ACommerce.SharedKernel.Domain.DynamicAttributes;

namespace ACommerce.Kits.DynamicAttributes.Backend;

/// <summary>
/// يُجَمِّع snapshot نِهائي لِكِيان مَن (template + AttributesJson).
/// المَنطِق يَلُفّ <see cref="DynamicAttributeHelper.BuildSnapshot"/> لكِن
/// يَستَقبِل JSON مُباشَرَةً ⇒ المُستَهلِكون لا يَحتاجون لِفَهم القاموس
/// الوَسيط.
/// </summary>
public static class AttributeSnapshotBuilder
{
    public static IReadOnlyList<DynamicAttribute> Build(
        AttributeTemplate? template, string? attributesJson)
    {
        if (template is null) return Array.Empty<DynamicAttribute>();
        var values = AttributeValues.Parse(attributesJson);
        return DynamicAttributeHelper.BuildSnapshot(template, values);
    }

    /// <summary>أَدّعى لِلسَيناريوهات الَّتي يَكون فيها الكِيان يُنَفِّذ
    /// <see cref="IHasDynamicAttributes"/> + لِلتَطبيق <c>IAttributeTemplateSource</c>.</summary>
    public static async Task<IReadOnlyList<DynamicAttribute>> BuildForAsync(
        IAttributeTemplateSource source, IHasDynamicAttributes entity, CancellationToken ct)
    {
        if (entity.DynamicAttributeScopeId is null) return Array.Empty<DynamicAttribute>();
        var tpl = await source.BuildForScopeAsync(entity.DynamicAttributeScopeId.Value, ct);
        return Build(tpl, entity.AttributesJson);
    }
}
