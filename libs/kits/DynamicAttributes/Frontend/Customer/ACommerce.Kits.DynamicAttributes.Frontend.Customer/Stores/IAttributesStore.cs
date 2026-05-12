using ACommerce.SharedKernel.Domain.DynamicAttributes;

namespace ACommerce.Kits.DynamicAttributes.Frontend.Customer.Stores;

/// <summary>
/// Store واجِهَة أَمامِيَّة لِلقَوالِب الديناميكِيَّة. يَعمَل cache في
/// الذاكِرَة لِأَنّ القَوالِب نادِراً ما تَتَغَيَّر داخِل جَلسَة واحِدَة.
/// التَطبيق يَربط implementation يَتَصِل بِالـ <c>/dynamic-attributes/templates/{scope}</c>.
/// </summary>
public interface IAttributesStore
{
    /// <summary>قالَب نِطاق مُعَيَّن. يُخَزَّن في cache بَعد أَوَّل جَلب.</summary>
    Task<AttributeTemplate?> GetTemplateAsync(Guid scopeId, CancellationToken ct = default);

    /// <summary>إِبطال الـ cache لِنِطاق مُعَيَّن (يُستَدعى بَعد admin edit).</summary>
    void InvalidateTemplate(Guid scopeId);
}
