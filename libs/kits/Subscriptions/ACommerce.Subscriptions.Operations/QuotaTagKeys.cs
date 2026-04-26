using ACommerce.OperationEngine.Core;

namespace ACommerce.Subscriptions.Operations;

/// <summary>
/// مفاتيح العلامات القياسية لمعترضات الحصة.
/// المتحكمات تستخدم هذه الأصناف بدلاً من النصوص للأمان النوعي.
///
/// مثال:
///   Entry.Create(SomeOp)
///     .Tag(QuotaTagKeys.Check, "listings.create")
///     .Tag(QuotaTagKeys.UserId, userId)
///     .Tag(QuotaTagKeys.ScopeKey, "listing_categories")
///     .Tag(QuotaTagKeys.ScopeValue, "residential")
/// </summary>
public static class QuotaTagKeys
{
    /// <summary>اسم نوع الحصة (مثل: "listings.create")</summary>
    public static readonly TagKey Check = new("quota_check");

    /// <summary>معرّف المستخدم للبحث عن اشتراكاته</summary>
    public static readonly TagKey UserId = new("quota_user_id");

    /// <summary>مفتاح النطاق الفرعي (مثل: "listing_categories")</summary>
    public static readonly TagKey ScopeKey = new("quota_scope_key");

    /// <summary>قيمة النطاق الفرعي (مثل: "residential")</summary>
    public static readonly TagKey ScopeValue = new("quota_scope_value");
}
