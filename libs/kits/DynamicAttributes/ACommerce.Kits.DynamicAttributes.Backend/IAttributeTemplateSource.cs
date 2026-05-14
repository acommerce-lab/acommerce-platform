using ACommerce.SharedKernel.Domain.DynamicAttributes;

namespace ACommerce.Kits.DynamicAttributes.Backend;

/// <summary>
/// المَنفَذ الَّذي يَجِب أَن يُنَفِّذه التَطبيق لِيَربِط النِطاق بِالقالَب.
/// التَطبيق يَختار كَيف يُخَزِّن التَعريفات (جَدول مَركَزي مَوجود، YAML،
/// JSON، …) — الكيت لا يَفرِض شَكلاً.
///
/// <para><b>ScopeId</b>: في Ashare ⇒ <c>ProductCategory.Id</c> لِلإعلانات،
/// و sentinel ثابِت لِـ Profile/Complaint/…  التَطبيق يُحَدِّد المَعنى.</para>
/// </summary>
public interface IAttributeTemplateSource
{
    /// <summary>يُرجِع القالَب لِنِطاق مُعَيَّن، أَو <c>null</c> إن لَم يوجَد.</summary>
    Task<AttributeTemplate?> BuildForScopeAsync(Guid scopeId, CancellationToken ct);
}
