using ACommerce.Kits.DynamicAttributes.Backend;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ejar.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace Ejar.Api.Controllers;

/// <summary>
/// نُقطَة وُصول الواجِهَة الأَمامِيَّة لِجَلب قالَب سِمات لِفِئَة مُعَيَّنَة
/// عَبر الـ slug. الـ Marketplace template يَتَوَقَّع هذا الشَكل
/// <c>/categories/{slug}/attribute-template</c> — مَوجود في V3 (CategoryTemplatesController)،
/// وأُضيفَ هُنا لِيَعمَل CreateListing/EditListing wizard مَع Ejar.
///
/// <para>الـ slug يَتَحَوَّل إلى Guid ثابِت عَبر
/// <see cref="EjarListingScopes.DeriveScopeId"/> (نَفس الخوارَزمِيَّة الَّتي
/// يَستَخدِمها <c>ListingEntity.DynamicAttributeScopeId</c>) ثُمّ يُجلَب
/// القالَب عَبر <see cref="IAttributeTemplateSource"/> (المُسَجَّل = EjarAttributeTemplateSource).</para>
/// </summary>
[ApiController]
public sealed class EjarCategoryTemplatesController : ControllerBase
{
    private readonly IAttributeTemplateSource _source;
    public EjarCategoryTemplatesController(IAttributeTemplateSource source) => _source = source;

    [HttpGet("/categories/{slug}/attribute-template")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return this.NotFoundEnvelope("slug_required");

        var scopeId = EjarListingScopes.DeriveScopeId(slug);
        var tpl = await _source.BuildForScopeAsync(scopeId, ct);
        // فِئَة بِلا قالَب ⇒ نَرُدّ template فارِغ (لا 404) — الواجِهَة
        // الأَمامِيَّة تَتَوَقَّع <c>OperationEnvelope&lt;AttributeTemplate&gt;</c>
        // ويُفَضَّل أَن يَتَعَرَّف Fields.Count == 0 بَدَل خَطَأ HTTP.
        var safe = tpl ?? new AttributeTemplate();
        return this.OkEnvelope("category.template.get", safe);
    }
}
