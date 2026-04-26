using ACommerce.OperationEngine.Core;
using ACommerce.SharedKernel.Abstractions.Repositories;
using ACommerce.Translations.Operations.Entities;
using ACommerce.Translations.Operations.Operations;

namespace ACommerce.Translations.Operations;

/// <summary>
/// واجهة المطور البسيطة للترجمات.
///
///   await translations.SetAsync("Listing", listingId, "Title", "en", "Luxury Apartment");
///   var title = await translations.GetAsync("Listing", listingId, "Title", "en", fallback: "ar");
/// </summary>
public class TranslationService
{
    private readonly IBaseAsyncRepository<Translation> _repo;
    private readonly OpEngine _engine;

    public TranslationService(IRepositoryFactory factory, OpEngine engine)
    {
        _repo = factory.CreateRepository<Translation>();
        _engine = engine;
    }

    /// <summary>تعيين ترجمة (upsert) عبر العملية المحاسبية</summary>
    public async Task<Guid?> SetAsync(
        string entityType,
        Guid entityId,
        string fieldName,
        string language,
        string translatedText,
        string? translatorId = null,
        bool isVerified = false,
        CancellationToken ct = default)
    {
        var op = TranslationOps.Set(_repo, entityType, entityId, fieldName, language, translatedText, translatorId, isVerified);
        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return null;
        return result.Context!.TryGet<Guid>("translationId", out var id) ? id : null;
    }

    /// <summary>الحصول على نص مترجَم - مع fallback للغة افتراضية إن لم يوجد</summary>
    public async Task<string?> GetAsync(
        string entityType,
        Guid entityId,
        string fieldName,
        string language,
        string? fallback = null,
        CancellationToken ct = default)
    {
        var matches = await _repo.GetAllWithPredicateAsync(t =>
            t.EntityType == entityType &&
            t.EntityId == entityId &&
            t.FieldName == fieldName &&
            t.Language == language);

        if (matches.Count > 0) return matches[0].TranslatedText;

        if (!string.IsNullOrEmpty(fallback) && fallback != language)
        {
            var fb = await _repo.GetAllWithPredicateAsync(t =>
                t.EntityType == entityType &&
                t.EntityId == entityId &&
                t.FieldName == fieldName &&
                t.Language == fallback);
            if (fb.Count > 0) return fb[0].TranslatedText;
        }

        return null;
    }

    /// <summary>كل ترجمات حقل (لكل اللغات)</summary>
    public async Task<IReadOnlyList<Translation>> GetAllForFieldAsync(
        string entityType,
        Guid entityId,
        string fieldName,
        CancellationToken ct = default)
    {
        return await _repo.GetAllWithPredicateAsync(t =>
            t.EntityType == entityType &&
            t.EntityId == entityId &&
            t.FieldName == fieldName);
    }

    /// <summary>كل ترجمات كيان (كل الحقول كل اللغات)</summary>
    public async Task<IReadOnlyList<Translation>> GetAllForEntityAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default)
    {
        return await _repo.GetAllWithPredicateAsync(t =>
            t.EntityType == entityType &&
            t.EntityId == entityId);
    }

    /// <summary>حذف ترجمة بمعرفها</summary>
    public async Task<bool> RemoveAsync(Guid translationId, string? actorId = null, CancellationToken ct = default)
    {
        var op = TranslationOps.Remove(_repo, translationId, actorId);
        var result = await _engine.ExecuteAsync(op, ct);
        return result.Success;
    }
}
