using System.Reflection;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ACommerce.Translations.Operations.Interceptors;

/// <summary>
/// مفاتيح العلامات القياسية للترجمة.
/// </summary>
public static class TranslationTagKeys
{
    /// <summary>مفتاح يُعلِن أن القيد يُنتج بيانات قابلة للترجمة.</summary>
    public static readonly TagKey Translatable = new("translatable");

    /// <summary>قائمة أسماء الحقول المترجَمة مفصولة بفواصل (مثال: "Title,Description").</summary>
    public static readonly TagKey Fields = new("translatable_fields");

    /// <summary>نوع الكيان المترجَم كما في TranslationEntity.EntityType (مثال: "Listing").</summary>
    public static readonly TagKey EntityType = new("translatable_entity_type");

    /// <summary>مفتاح الكيان في ctx.Items (افتراضي: entity).</summary>
    public static readonly TagKey ContextKey = new("translatable_context_key");
}

/// <summary>
/// معترض الترجمة (PostInterceptor):
///
/// ينطبق على أي قيد عليه علامة translatable. يعمل بعد التنفيذ:
///   1. يقرأ Accept-Language من HttpContext
///   2. يجلب الكيان من ctx.Items حسب translatable_context_key
///   3. يحدّد الحقول المترجَمة من translatable_fields (CSV)
///   4. يستدعي TranslationService.GetAsync لكل حقل × لغة المستخدم
///   5. يستبدل قيم الحقول بالنصوص المترجَمة مباشرة على الكائن
///
/// النتيجة: المتحكم لا يعرف شيئاً عن الترجمة. يكفي وضع علامة واحدة.
/// الترجمة تصبح "غلاف" شفاف حول كل عملية.
/// </summary>
public class TranslationInterceptor : IOperationInterceptor
{
    private readonly TranslationService _translations;
    private readonly IHttpContextAccessor _httpContext;
    private readonly ILogger<TranslationInterceptor> _logger;
    private readonly string _fallbackLanguage;

    public string Name => "TranslationInterceptor";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public TranslationInterceptor(
        TranslationService translations,
        IHttpContextAccessor httpContext,
        ILogger<TranslationInterceptor> logger,
        string fallbackLanguage = "ar")
    {
        _translations = translations;
        _httpContext = httpContext;
        _logger = logger;
        _fallbackLanguage = fallbackLanguage;
    }

    public bool AppliesTo(Operation op) => op.HasTag(TranslationTagKeys.Translatable.Name);

    public async Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
    {
        var op = context.Operation;

        // 1) حدد اللغة المطلوبة من Accept-Language
        var requestedLang = ResolveLanguage();
        if (requestedLang == _fallbackLanguage)
            return AnalyzerResult.Pass();  // لا حاجة لأي ترجمة

        // 2) الحقول المترجَمة
        var fieldsTag = op.GetTagValue(TranslationTagKeys.Fields.Name);
        if (string.IsNullOrEmpty(fieldsTag))
            return AnalyzerResult.Warning("no_translatable_fields");

        var fields = fieldsTag.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length == 0)
            return AnalyzerResult.Warning("empty_field_list");

        // 3) نوع الكيان
        var entityTypeName = op.GetTagValue(TranslationTagKeys.EntityType.Name);
        if (string.IsNullOrEmpty(entityTypeName))
            return AnalyzerResult.Warning("no_entity_type");

        // 4) الكيان من ctx
        var contextKey = op.GetTagValue(TranslationTagKeys.ContextKey.Name) ?? "entity";
        if (!context.Items.TryGetValue(contextKey, out var entity) || entity == null)
            return AnalyzerResult.Warning($"no_entity_in_context:{contextKey}");

        // 5) معرّف الكيان (Id)
        var idProperty = entity.GetType().GetProperty("Id");
        if (idProperty == null || idProperty.PropertyType != typeof(Guid))
            return AnalyzerResult.Warning("entity_without_guid_id");

        var entityId = (Guid)idProperty.GetValue(entity)!;

        // 6) استبدل كل حقل بالترجمة إن وُجدت
        int translatedCount = 0;
        foreach (var fieldName in fields)
        {
            var propInfo = entity.GetType().GetProperty(fieldName,
                BindingFlags.Public | BindingFlags.Instance);
            if (propInfo == null || propInfo.PropertyType != typeof(string) || !propInfo.CanWrite)
                continue;

            var translated = await _translations.GetAsync(
                entityTypeName, entityId, fieldName, requestedLang,
                fallback: null,  // لا نريد fallback هنا - نحتفظ بالأصل إن لم توجد ترجمة
                ct: context.CancellationToken);

            if (!string.IsNullOrEmpty(translated))
            {
                propInfo.SetValue(entity, translated);
                translatedCount++;
            }
        }

        return new AnalyzerResult
        {
            Passed = true,
            Message = $"translated_{translatedCount}_fields_to_{requestedLang}",
            Data = new Dictionary<string, object>
            {
                ["language"] = requestedLang,
                ["translated_fields"] = translatedCount,
                ["entity_type"] = entityTypeName
            }
        };
    }

    private string ResolveLanguage()
    {
        var ctx = _httpContext.HttpContext;
        if (ctx == null) return _fallbackLanguage;

        // نحاول header Accept-Language أولاً
        var acceptLang = ctx.Request.Headers["Accept-Language"].FirstOrDefault();
        if (!string.IsNullOrEmpty(acceptLang))
        {
            // Accept-Language قد يحوي عدة لغات: "en-US,en;q=0.9,ar;q=0.8"
            var primary = acceptLang.Split(',').FirstOrDefault()?.Split(';').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(primary))
            {
                // نأخذ أول مقطعين فقط (مثلاً en من en-US)
                return primary.Split('-')[0].ToLowerInvariant();
            }
        }

        // نحاول query string ?lang=en
        var queryLang = ctx.Request.Query["lang"].FirstOrDefault();
        if (!string.IsNullOrEmpty(queryLang))
            return queryLang.ToLowerInvariant();

        return _fallbackLanguage;
    }
}
