namespace ACommerce.Authentication.TwoFactor.Operations.Abstractions;

/// <summary>
/// قناة المصادقة الثنائية.
/// كل مزود (SMS, Email, Nafath, TOTP, Push) يطبقها.
/// </summary>
public interface ITwoFactorChannel
{
    /// <summary>اسم القناة: "sms", "email", "nafath", "totp"</summary>
    string Name { get; }

    /// <summary>هل القناة تتطلب سر مُولّد (code) أم عملية خارجية (Nafath)؟</summary>
    bool GeneratesCode { get; }

    /// <summary>
    /// إطلاق تحدي: إرسال رمز أو بدء عملية التحقق.
    /// يُرجع معرف التحدي (challenge ID) ليُستخدم لاحقاً في التحقق.
    /// </summary>
    Task<ChallengeResult> InitiateAsync(
        string userIdentifier,
        string? target = null,
        CancellationToken ct = default);

    /// <summary>
    /// التحقق من تحدي:
    /// - للقنوات التي تُولد كود: تطابق الكود مع المخزون.
    /// - للقنوات الخارجية: استعلام المزود عن حالة التحدي.
    /// </summary>
    Task<VerificationResult> VerifyAsync(
        string challengeId,
        string? providedCode = null,
        CancellationToken ct = default);
}

/// <summary>
/// مخزن التحديات - لا كيان.
/// يحفظ التحديات النشطة وحالتها.
/// </summary>
public interface IChallengeStore
{
    Task SaveAsync(Challenge challenge, CancellationToken ct = default);
    Task<Challenge?> GetAsync(string challengeId, CancellationToken ct = default);
    Task UpdateStatusAsync(string challengeId, ChallengeStatus status, CancellationToken ct = default);
    Task RemoveAsync(string challengeId, CancellationToken ct = default);
    Task<int> RemoveExpiredAsync(CancellationToken ct = default);
}

/// <summary>
/// نتيجة إطلاق تحدي.
/// </summary>
public record ChallengeResult(
    bool Succeeded,
    string ChallengeId,
    string? Error = null,
    Dictionary<string, string>? ProviderData = null);

/// <summary>
/// نتيجة التحقق من تحدي.
/// </summary>
public record VerificationResult(
    bool Verified,
    string? Reason = null,
    Dictionary<string, string>? ProviderData = null);
