using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using System.Collections.Concurrent;

namespace ACommerce.Authentication.TwoFactor.Providers.Sms.Mock;

/// <summary>
/// قناة OTP تجريبية للرسائل القصيرة — الرمز ثابت دائماً <c>123456</c>.
///
/// تُستبدَل بـ <c>SmsTwoFactorChannel</c> (مع مزود SMS حقيقي) عند الإنتاج.
/// لا ترسل أي رسالة فعلية ولا تعتمد على شبكة.
///
/// السلوك:
/// <list type="bullet">
///   <item>InitiateAsync → يسجّل التحدي برقم الجوال ومدة 120 ثانية.</item>
///   <item>VerifyAsync   → يقبل فقط الرمز "123456"، ويتسامح مع الجلسات غير المُبدأة (auto-seed).</item>
/// </list>
/// </summary>
public sealed class MockSmsTwoFactorChannel : ITwoFactorChannel
{
    private const string FixedCode = "123456";
    private readonly ConcurrentDictionary<string, DateTimeOffset> _pending = new();

    public string Name => "sms";
    public bool GeneratesCode => true;

    public Task<ChallengeResult> InitiateAsync(
        string userIdentifier,
        string? target = null,
        CancellationToken ct = default)
    {
        var phone = (target ?? userIdentifier).Trim();
        if (string.IsNullOrEmpty(phone))
            return Task.FromResult(new ChallengeResult(false, "", "phone_required"));

        _pending[phone] = DateTimeOffset.UtcNow.AddSeconds(120);

        return Task.FromResult(new ChallengeResult(
            Succeeded: true,
            ChallengeId: phone,
            ProviderData: new Dictionary<string, string> { ["masked"] = MaskPhone(phone) }));
    }

    public Task<VerificationResult> VerifyAsync(
        string challengeId,
        string? providedCode = null,
        CancellationToken ct = default)
    {
        // Auto-seed: يتسامح مع التحقق المباشر دون RequestOtp سابق (سلوك المحاكاة).
        if (!_pending.ContainsKey(challengeId))
            _pending[challengeId] = DateTimeOffset.UtcNow.AddSeconds(120);

        if (providedCode != FixedCode)
            return Task.FromResult(new VerificationResult(false, "wrong_code"));

        if (!_pending.TryGetValue(challengeId, out var exp) || exp < DateTimeOffset.UtcNow)
        {
            _pending.TryRemove(challengeId, out _);
            return Task.FromResult(new VerificationResult(false, "expired"));
        }

        _pending.TryRemove(challengeId, out _);
        return Task.FromResult(new VerificationResult(true));
    }

    private static string MaskPhone(string p) =>
        p.Length > 4 ? $"****{p[^4..]}" : "****";
}
