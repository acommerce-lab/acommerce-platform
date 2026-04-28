namespace ACommerce.Kits.Auth.Operations;

/// <summary>
/// مفاتيح التاجات المستخدمة من <see cref="AuthGateInterceptor"/>.
/// نفس النمط المستخدم في <c>VersionTagKeys</c> و<c>QuotaTagKeys</c>: التطبيق يضع
/// التاج على القيد، والمعترض يقرّر السلوك.
/// </summary>
public static class AuthTagKeys
{
    /// <summary>
    /// تاج صريح يطلب من المعترض فحص أن المستدعي مصادَق عليه. لو غاب التاج،
    /// المعترض لا يفعل شيئاً (يتركه لـ <c>[Authorize]</c>) — هذا يجعل الانتقال
    /// تدريجياً وآمناً في القواعد الكوديّة الكبيرة.
    /// </summary>
    public const string RequiresAuth = "requires_auth";

    /// <summary>
    /// يوضع على عمليّات <c>auth.*</c> ذاتها لتجاوز معترض المصادقة (لأنّ هدفها
    /// إنشاء جلسة المصادقة أصلاً).
    /// </summary>
    public const string SkipAuthGate = "skip_auth_gate";

    /// <summary>كود الرفض القياسيّ الذي يستخدمه المعترض.</summary>
    public const string RejectionCode_NotAuthenticated = "auth_required";
}
