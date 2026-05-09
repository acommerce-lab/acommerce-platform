namespace ACommerce.Kits.Subscriptions.Backend;

/// <summary>
/// خيارات Subscriptions kit — تُمرَّر في <see cref="SubscriptionsKitExtensions.AddSubscriptionsKit"/>.
/// </summary>
public sealed class SubscriptionsKitOptions
{
    /// <summary>
    /// وضع التجربة المفتوحة. لو <c>true</c>:
    /// <list type="bullet">
    ///   <item><c>GET /me/subscription</c> يردّ اشتراكاً اصطناعيّاً نشطاً
    ///         (<see cref="TrialPlanName"/>، <see cref="TrialDurationYears"/>،
    ///         حدود = ٠ = unlimited) <i>دون لمس DB</i>.</item>
    ///   <item>الـ <c>SubscriptionGateInterceptor</c> ما يزال يعمل لكنّه
    ///         يجد الاشتراك الاصطناعيّ نشطاً (التطبيق المضيف هو من يخفّض
    ///         الـ gate لو رغب).</item>
    /// </list>
    /// افتراضيّاً <c>false</c> — التطبيق المعتمد على الباقات يفعّله صراحةً
    /// في فترة الإطلاق التجريبيّ، وعند تحوّله لنموذج مدفوع يُسقطه بسطر واحد
    /// في <c>appsettings.json</c> أو <c>AddSubscriptionsKit(opts)</c>.
    /// </summary>
    public bool OpenAccess { get; set; } = false;

    /// <summary>اسم الباقة الاصطناعيّة في وضع التجربة.</summary>
    public string TrialPlanName { get; set; } = "تجربة مفتوحة";

    /// <summary>مدّة الاشتراك الاصطناعيّ بالسنوات (ENDDate = اليوم + N).</summary>
    public int TrialDurationYears { get; set; } = 10;
}
