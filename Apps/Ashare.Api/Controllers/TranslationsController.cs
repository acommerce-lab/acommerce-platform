using ACommerce.Translations.Operations;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Api.Controllers;

[ApiController]
[Route("api/translations")]
public class TranslationsController : ControllerBase
{
    private readonly TranslationService _service;

    public TranslationsController(TranslationService service) => _service = service;

    public record SetTranslationRequest(
        string EntityType,
        Guid EntityId,
        string FieldName,
        string Language,
        string TranslatedText,
        string? TranslatorId,
        bool IsVerified);

    [HttpPost]
    public async Task<IActionResult> Set([FromBody] SetTranslationRequest req, CancellationToken ct)
    {
        var id = await _service.SetAsync(
            req.EntityType, req.EntityId, req.FieldName,
            req.Language, req.TranslatedText,
            req.TranslatorId, req.IsVerified, ct);

        if (id == null) return this.BadRequestEnvelope("translation_set_failed");
        return this.OkEnvelope("translation.set", new { translationId = id });
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        [FromQuery] string fieldName,
        [FromQuery] string language,
        [FromQuery] string? fallback = "ar",
        CancellationToken ct = default)
    {
        var text = await _service.GetAsync(entityType, entityId, fieldName, language, fallback, ct);
        if (text == null) return this.NotFoundEnvelope("translation_not_found");
        return this.OkEnvelope("translation.get", new { text, language });
    }

    [HttpGet("entity/{entityType}/{entityId:guid}")]
    public async Task<IActionResult> AllForEntity(string entityType, Guid entityId, CancellationToken ct)
    {
        var list = await _service.GetAllForEntityAsync(entityType, entityId, ct);
        return this.OkEnvelope("translation.list.entity", list.ToList());
    }

    [HttpGet("field/{entityType}/{entityId:guid}/{fieldName}")]
    public async Task<IActionResult> AllForField(string entityType, Guid entityId, string fieldName, CancellationToken ct)
    {
        var list = await _service.GetAllForFieldAsync(entityType, entityId, fieldName, ct);
        return this.OkEnvelope("translation.list.field", list.ToList());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string? actorId, CancellationToken ct)
    {
        var ok = await _service.RemoveAsync(id, actorId, ct);
        return ok
            ? this.NoContentEnvelope("translation.delete")
            : this.NotFoundEnvelope("translation_not_found");
    }
}
