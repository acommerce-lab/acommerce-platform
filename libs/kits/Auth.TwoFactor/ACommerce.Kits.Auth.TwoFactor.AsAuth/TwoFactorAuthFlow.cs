using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.Kits.Auth.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Auth.TwoFactor.AsAuth;

/// <summary>
/// Adapts an <see cref="ITwoFactorChannel"/> into an <see cref="IAuthFlow"/>.
/// Initiate sends the OTP via the 2FA channel; Complete verifies the code.
/// All Auth-Kit knowledge of "OTP" lives here — both the AuthController
/// (in Auth.Backend) and the 2FA channel itself remain unaware of each other.
/// </summary>
public sealed class TwoFactorAuthFlow : IAuthFlow
{
    private const int OtpExpirySeconds = 120;
    private readonly ITwoFactorChannel _channel;

    public TwoFactorAuthFlow(ITwoFactorChannel channel) { _channel = channel; }

    public async Task<AuthInitiateResult> InitiateAsync(string subject, CancellationToken ct)
    {
        try
        {
            await _channel.InitiateAsync(subject, subject, ct);
            return new AuthInitiateResult(Ok: true, ExpiresInSeconds: OtpExpirySeconds);
        }
        catch (Exception ex)
        {
            return new AuthInitiateResult(Ok: false, Reason: ex.Message);
        }
    }

    public async Task<AuthCompleteResult> CompleteAsync(string subject, string secret, CancellationToken ct)
    {
        var r = await _channel.VerifyAsync(subject, secret, ct);
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
        services.AddScoped<IAuthFlow, TwoFactorAuthFlow>();
        return services;
    }
}
