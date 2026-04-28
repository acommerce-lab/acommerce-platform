namespace ACommerce.Kits.Auth.Operations;

/// <summary>
/// تجريد الـ Auth flow. الـ <c>AuthController</c> يحرّكه؛ التطبيق يحقن
/// تطبيقاً ملموساً يحدّد كيفيّة التحقّق:
/// <list type="bullet">
///   <item>OTP عبر SMS — يأتي من حزمة الجسر <c>Auth.TwoFactor.AsAuth</c>
///         التي تستهلك <c>ITwoFactorChannel</c> داخليّاً.</item>
///   <item>Magic-link عبر بريد — تطبيق آخر لـ <c>IAuthFlow</c>.</item>
///   <item>WebAuthn / passkey — تطبيق ثالث.</item>
/// </list>
/// الـ Auth Kit نفسه لا يعرف أيّاً منها — هذا الفصل هو نقطة الفائدة.
/// </summary>
public interface IAuthFlow
{
    /// <summary>
    /// يبدأ التحقّق (مثلاً يرسل OTP). <paramref name="subject"/> هو معرّف
    /// المستخدم الخارجيّ (هاتف، بريد، NationalId).
    /// </summary>
    Task<AuthInitiateResult> InitiateAsync(string subject, CancellationToken ct);

    /// <summary>
    /// يكمل التحقّق بقدر <paramref name="secret"/> (OTP code، token، …).
    /// عند النجاح يرجع subject الذي أكملت عليه + معلومات إضافيّة اختياريّة.
    /// </summary>
    Task<AuthCompleteResult> CompleteAsync(string subject, string secret, CancellationToken ct);
}

/// <param name="Ok">هل بدأ التحقّق بنجاح.</param>
/// <param name="ExpiresInSeconds">المدّة المتبقّية لإكمال التحقّق (للـ OTP عادةً 60-300 ث).</param>
/// <param name="Reason">رمز سبب الفشل (لتقدّم رسالة UI واضحة).</param>
public sealed record AuthInitiateResult(bool Ok, int ExpiresInSeconds = 0, string? Reason = null);

/// <param name="Verified">هل قُبل الـ secret.</param>
/// <param name="Subject">المستخدم الخارجيّ المُتَحقَّق منه (للـ AuthController يحوّله إلى userId عبر <see cref="IAuthUserStore"/>).</param>
/// <param name="Reason">رمز سبب الفشل.</param>
public sealed record AuthCompleteResult(bool Verified, string Subject = "", string? Reason = null);
