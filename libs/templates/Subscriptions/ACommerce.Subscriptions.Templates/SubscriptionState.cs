namespace ACommerce.Subscriptions.Templates;

/// <summary>
/// حامل حالة الاشتراك للواجهة. Singleton (Server) أو Scoped (WASM).
/// التطبيق يستدعي <see cref="Set"/> بعد التحقّق من الاشتراك (مثلاً عند تسجيل
/// الدخول أو عند فتح الصفحة المحميّة)، والـ <see cref="AcSubscriptionGate"/>
/// يقرأ القيمة لاتخاذ قرار العرض.
///
/// <para>الفرق عن <c>VersionState</c>: لا توجد دالّة <c>RefreshAsync</c> هنا
/// لأنّ الاشتراك data-source-specific؛ التطبيق يقرّر متى يحدّثه.</para>
/// </summary>
public sealed class SubscriptionState
{
    public bool? HasActive { get; private set; }
    public string? PlanName { get; private set; }
    public DateTime? ExpiresAt { get; private set; }

    public bool Loaded => HasActive.HasValue;
    public event Action? Changed;

    public void Set(bool hasActive, string? planName = null, DateTime? expiresAt = null)
    {
        HasActive = hasActive;
        PlanName  = planName;
        ExpiresAt = expiresAt;
        Changed?.Invoke();
    }

    public void Clear()
    {
        HasActive = null;
        PlanName  = null;
        ExpiresAt = null;
        Changed?.Invoke();
    }
}
