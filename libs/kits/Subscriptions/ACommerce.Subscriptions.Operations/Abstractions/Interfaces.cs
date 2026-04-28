using ACommerce.SharedKernel.Domain.Entities;

namespace ACommerce.Subscriptions.Operations.Abstractions;

/// <summary>
/// واجهة الباقة - أي تطبيق يُطبقها على كيانه المادي.
/// لا توجد كيانات concrete في هذه المكتبة.
///
/// مثال:
///   public class MyAppPlan : IBaseEntity, IPlan {
///       // الحقول الأساسية من IPlan
///       public Guid Id { get; set; }
///       ...
///   }
/// </summary>
public interface IPlan : IBaseEntity
{
    string Slug { get; set; }
    string Name { get; set; }
    bool IsActive { get; set; }

    /// <summary>
    /// الحصص بصيغة dictionary - مفتاح لكل نوع quota.
    /// مثال: { "listings.create": 5, "messages.send": -1, "notifications.send": 100 }
    /// القيمة -1 = غير محدود.
    /// </summary>
    Dictionary<string, int> Quotas { get; set; }

    /// <summary>
    /// الفئات/الأنواع المسموح بها (CSV أو list).
    /// مفتاح لكل بُعد تصنيفي. مثال:
    ///   { "listing_categories": "residential,commercial" }
    /// </summary>
    Dictionary<string, string> AllowedScopes { get; set; }
}

/// <summary>
/// واجهة الاشتراك - يطبقها التطبيق على كيانه المادي.
/// </summary>
public interface ISubscription : IBaseEntity
{
    Guid UserId { get; set; }
    Guid PlanId { get; set; }
    DateTime StartDate { get; set; }
    DateTime EndDate { get; set; }

    /// <summary>هل الاشتراك نشط الآن (status + dates)</summary>
    bool IsCurrentlyActive { get; }

    /// <summary>
    /// عدّادات الاستخدام الحالية - تُزاد عبر QuotaConsumptionInterceptor.
    /// المفتاح يطابق quota key في IPlan.Quotas
    /// </summary>
    Dictionary<string, int> Used { get; set; }
}

/// <summary>
/// مزوّد الاشتراكات - تجريد للتطبيق ليُخبر المكتبة كيف تجد البيانات.
/// التطبيق يسجّل تطبيقه في DI، والمعترضات تستخدمه دون معرفة الكيانات المادية.
/// </summary>
public interface ISubscriptionProvider
{
    /// <summary>كل اشتراكات المستخدم النشطة مرتّبة من الأقدم للأحدث</summary>
    Task<IReadOnlyList<ISubscription>> GetActiveSubscriptionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>الباقة بمعرّفها</summary>
    Task<IPlan?> GetPlanAsync(Guid planId, CancellationToken ct = default);

    /// <summary>تحديث اشتراك بعد استهلاك حصة</summary>
    Task UpdateSubscriptionAsync(ISubscription sub, CancellationToken ct = default);
}
