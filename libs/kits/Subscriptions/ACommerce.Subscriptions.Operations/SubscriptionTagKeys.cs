namespace ACommerce.Subscriptions.Operations;

/// <summary>
/// مفاتيح التاجات المستخدمة من <see cref="SubscriptionGateInterceptor"/>.
/// مكمّلة لـ <see cref="QuotaTagKeys"/>: تلك للحصص الفرديّة، هذه للبوّابة العامّة
/// "هل لديك أيّ اشتراك نشط أصلاً؟".
/// </summary>
public static class SubscriptionTagKeys
{
    /// <summary>
    /// تاج يطلب من المعترض رفض القيد لو لم يكن للمستخدم أيّ اشتراك نشط.
    /// </summary>
    public const string RequiresActiveSubscription = "requires_subscription";

    /// <summary>
    /// تاج يوضع على عمليّات <c>subscription.*</c> (الإدارة الذاتيّة) لتجاوز
    /// المعترض. عمليّات <c>subscription.*</c> تتجاوزه افتراضياً بناءً على نوعها.
    /// </summary>
    public const string SkipSubscriptionGate = "skip_subscription_gate";

    /// <summary>كود الرفض القياسيّ.</summary>
    public const string RejectionCode_NoActiveSubscription = "subscription_required";
}
