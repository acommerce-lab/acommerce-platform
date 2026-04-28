using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Repositories.Interfaces;
using ACommerce.Translations.Operations.Entities;

namespace ACommerce.Translations.Operations.Operations;

/// <summary>
/// مفاتيح علامات الترجمات.
/// </summary>
public static class TranslationTags
{
    public static readonly TagKey EntityType = new("entity_type");
    public static readonly TagKey EntityId = new("entity_id");
    public static readonly TagKey FieldName = new("field_name");
    public static readonly TagKey Language = new("language");
    public static readonly TagKey Translator = new("translator");
    public static readonly TagKey Verified = new("verified");
    public static readonly TagKey Role = new("role");
}

/// <summary>
/// قيود الترجمات - كل تعيين ترجمة = قيد بين المترجم والكيان المترجَم.
/// </summary>
public static class TranslationOps
{
    /// <summary>
    /// قيد: تعيين ترجمة لحقل في كيان.
    /// المترجم (مدين) → الكيان/الحقل (دائن).
    /// </summary>
    public static Operation Set(
        IBaseAsyncRepository<Translation> repo,
        string entityType,
        Guid entityId,
        string fieldName,
        string language,
        string translatedText,
        string? translatorId = null,
        bool isVerified = false)
    {
        return Entry.Create("translation.set")
            .Describe($"Translate {entityType}:{entityId}.{fieldName} to {language}")
            .From($"Translator:{translatorId ?? "system"}", 1, (TranslationTags.Role, "translator"))
            .To($"{entityType}:{entityId}", 1,
                (TranslationTags.Role, "target"),
                (TranslationTags.FieldName, fieldName),
                (TranslationTags.Language, language))
            .Tag(TranslationTags.EntityType, entityType)
            .Tag(TranslationTags.EntityId, entityId.ToString())
            .Tag(TranslationTags.FieldName, fieldName)
            .Tag(TranslationTags.Language, language)
            .Tag(TranslationTags.Verified, isVerified.ToString().ToLowerInvariant())
            // محلل: لا يقبل ترجمة فارغة
            .Analyze(new RequiredFieldAnalyzer("translated_text", () => translatedText))
            .Analyze(new RequiredFieldAnalyzer("language", () => language))
            .Execute(async ctx =>
            {
                // upsert: إذا كانت موجودة حدّثها وإلا أضفها
                var existing = await repo.GetAllWithPredicateAsync(t =>
                    t.EntityType == entityType &&
                    t.EntityId == entityId &&
                    t.FieldName == fieldName &&
                    t.Language == language);

                Translation translation;
                if (existing.Count > 0)
                {
                    translation = existing[0];
                    translation.TranslatedText = translatedText;
                    translation.IsVerified = isVerified;
                    translation.TranslatorId = translatorId;
                    await repo.UpdateAsync(translation, ctx.CancellationToken);
                }
                else
                {
                    translation = new Translation
                    {
                        Id = Guid.NewGuid(),
                        CreatedAt = DateTime.UtcNow,
                        EntityType = entityType,
                        EntityId = entityId,
                        FieldName = fieldName,
                        Language = language,
                        TranslatedText = translatedText,
                        IsVerified = isVerified,
                        TranslatorId = translatorId
                    };
                    await repo.AddAsync(translation, ctx.CancellationToken);
                }

                ctx.Set("translationId", translation.Id);
            })
            .Build();
    }

    /// <summary>
    /// قيد: حذف ترجمة.
    /// </summary>
    public static Operation Remove(
        IBaseAsyncRepository<Translation> repo,
        Guid translationId,
        string? actorId = null)
    {
        return Entry.Create("translation.remove")
            .From($"Translation:{translationId}", 1)
            .To($"Actor:{actorId ?? "system"}", 1)
            .Execute(async ctx =>
            {
                await repo.SoftDeleteAsync(translationId, ctx.CancellationToken);
            })
            .Build();
    }
}
