using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Mvc;

namespace ACommerce.Kits.DynamicAttributes.Backend;

/// <summary>
/// مَتَحَكِّم الكيت: واجِهَة عامَّة لِلواجِهات الأَمامِيَّة لِتَحميل
/// القَوالِب. التَطبيق يُسَجِّل <see cref="IAttributeTemplateSource"/>
/// عَبر DI ⇒ يَحصُل عَلى نُقطَة /dynamic-attributes/templates/{scopeId}
/// مَجّاناً.
/// </summary>
[ApiController]
[Route("dynamic-attributes")]
public sealed class DynamicAttributesController : ControllerBase
{
    private readonly IAttributeTemplateSource _source;
    public DynamicAttributesController(IAttributeTemplateSource source) => _source = source;

    /// <summary>قالَب نِطاق مُعَيَّن (مَيتاداتا فَقَط، بِلا قِيَم).</summary>
    [HttpGet("templates/{scopeId:guid}")]
    public async Task<IActionResult> GetTemplate(Guid scopeId, CancellationToken ct)
    {
        var tpl = await _source.BuildForScopeAsync(scopeId, ct);
        if (tpl is null) return this.NotFoundEnvelope("template_not_found");
        return this.OkEnvelope("dynamic_attrs.template.get", tpl);
    }
}
