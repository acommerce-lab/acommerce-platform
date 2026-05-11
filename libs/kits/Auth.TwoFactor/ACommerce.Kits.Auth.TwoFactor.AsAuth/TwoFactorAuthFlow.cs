using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.Kits.Auth.Operations;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace ACommerce.Kits.Auth.TwoFactor.AsAuth;

/// <summary>
/// ذاكِرَة في الـ process لِربط <c>subject</c> (هاتف، National-ID…) بِـ
/// <c>challengeId</c> الَّذي أَرجَعَه الـ provider في <c>InitiateAsync</c>.
/// مَطلوبَة لِأَنّ AuthController يَعرِف subject فَقَط في الـ Verify call،
/// لكِنّ بَعض الـ providers (مِثل Nafath) تَستَخدِم challengeId مُنفَصِل (Guid).
///
/// <para>Singleton — يَجِب أَن يَنجو بَين طَلَبَي Initiate/Verify المُختَلِفَين.</para>
/// </summary>
public sealed class ChallengeIdCache
{
    private readonly ConcurrentDictionary<string, (string ChallengeId, DateTimeOffset ExpiresAt)> _map = new();

    public void Set(string subject, string challengeId, int ttlSeconds)
    {
        _map[subject] = (challengeId, DateTimeOffset.UtcNow.AddSeconds(ttlSeconds));
    }

    public string? Get(string subject)
    {
        if (!_map.TryGetValue(subject, out var v)) return null;
        if (v.ExpiresAt < DateTimeOffset.UtcNow) { _map.TryRemove(subject, out _); return null; }
        return v.ChallengeId;
    }

    public void Remove(string subject) => _map.TryRemove(subject, out _);
}

/// <summary>
/// Adapts an <see cref="ITwoFactorChannel"/> into an <see cref="IAuthFlow"/>.
/// Initiate sends the OTP via the 2FA channel and captures its
/// <c>ChallengeId</c> + <c>ProviderData</c> (e.g. Nafath's <c>displayCode</c>).
/// Complete looks the challengeId back up via <see cref="ChallengeIdCache"/>
/// — Auth-Kit and AuthController stay channel-agnostic.
/// </summary>
public sealed class TwoFactorAuthFlow : IAuthFlow
{
    private const int OtpExpirySeconds = 120;
    private readonly ITwoFactorChannel _channel;
    private readonly ChallengeIdCache _cache;

    public TwoFactorAuthFlow(ITwoFactorChannel channel, ChallengeIdCache cache)
    {
        _channel = channel;
        _cache   = cache;
    }

    public async Task<AuthInitiateResult> InitiateAsync(string subject, CancellationToken ct)
    {
        try
        {
            var r = await _channel.InitiateAsync(subject, subject, ct);
            if (!r.Succeeded)
                return new AuthInitiateResult(Ok: false, Reason: r.Error);

            // اِحفَظ challengeId المُولَّد لِـ VerifyAsync (الفَرق الجَوهَري: لِـ
            // SMS الـ channel يَستَخدِم phone كَـ challengeId، لكِنّ Nafath
            // يُولِّد Guid مُستَقِلّ ⇒ لا بُدّ مِن cache).
            _cache.Set(subject, r.ChallengeId, OtpExpirySeconds);

            var expires = r.ProviderData != null
                       && r.ProviderData.TryGetValue("expiresInSeconds", out var exp)
                       && int.TryParse(exp, out var e)
                ? e
                : OtpExpirySeconds;

            return new AuthInitiateResult(
                Ok:               true,
                ExpiresInSeconds: expires,
                ChallengeId:      r.ChallengeId,
                ProviderData:     r.ProviderData);
        }
        catch (Exception ex)
        {
            return new AuthInitiateResult(Ok: false, Reason: ex.Message);
        }
    }

    public async Task<AuthCompleteResult> CompleteAsync(string subject, string secret, CancellationToken ct)
    {
        var challengeId = _cache.Get(subject) ?? subject; // SMS-style fallback
        var r = await _channel.VerifyAsync(challengeId, secret, ct);
        if (r.Verified) _cache.Remove(subject);
        return new AuthCompleteResult(
            Verified: r.Verified,
            Subject:  subject,
            Reason:   r.Reason);
    }
}

public static class TwoFactorAsAuthExtensions
{
    /// <summary>
    /// يربط <see cref="ITwoFactorChannel"/> المُسجَّل (مثلاً عبر
    /// <c>AddMockSmsTwoFactor</c>) كـ <see cref="IAuthFlow"/> — يجعل OTP-style
    /// login يعمل عبر الـ Auth Kit.
    /// </summary>
    public static IServiceCollection AddTwoFactorAsAuth(this IServiceCollection services)
    {
        services.AddSingleton<ChallengeIdCache>();
        services.AddScoped<IAuthFlow, TwoFactorAuthFlow>();
        return services;
    }
}
