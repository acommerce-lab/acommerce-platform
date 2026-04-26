using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ACommerce.Authentication.TwoFactor.Providers.Sms;

/// <summary>
/// واجهة مُرسل SMS. المطور يُطبقها للإنتاج.
/// في الوضع التجريبي يكفي تسجيل الكود.
/// </summary>
public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message, CancellationToken ct = default);
}

/// <summary>
/// مُرسل SMS وهمي - يسجل الكود في الـ logs فقط.
/// للتطوير والاختبار.
/// </summary>
public class LoggingSmsSender : ISmsSender
{
    private readonly ILogger<LoggingSmsSender> _logger;
    public LoggingSmsSender(ILogger<LoggingSmsSender> logger) => _logger = logger;

    public Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        _logger.LogWarning("═══════════════════════════════════════════════");
        _logger.LogWarning("📱 [DUMMY SMS] to: {Phone}", phoneNumber);
        _logger.LogWarning("📱 [DUMMY SMS] message: {Message}", message);
        _logger.LogWarning("═══════════════════════════════════════════════");
        return Task.CompletedTask;
    }
}

/// <summary>
/// قناة 2FA عبر SMS - تجريبية.
/// تُولّد كود 6 أرقام، ترسله عبر ISmsSender، وتتحقق من التطابق محلياً.
/// </summary>
public class SmsTwoFactorChannel : ITwoFactorChannel
{
    private readonly ISmsSender _sender;
    private readonly ILogger<SmsTwoFactorChannel> _logger;

    // challengeId → (code, phoneNumber, expiresAt)
    private readonly ConcurrentDictionary<string, PendingCode> _codes = new();

    public string Name => "sms";
    public bool GeneratesCode => true;

    public SmsTwoFactorChannel(ISmsSender sender, ILogger<SmsTwoFactorChannel> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChallengeResult> InitiateAsync(
        string userIdentifier,
        string? target = null,
        CancellationToken ct = default)
    {
        var phone = target ?? userIdentifier;
        if (string.IsNullOrWhiteSpace(phone))
            return new ChallengeResult(false, "", "phone_required");

        var code = GenerateCode();
        var challengeId = Guid.NewGuid().ToString("N");

        _codes[challengeId] = new PendingCode(code, phone, DateTimeOffset.UtcNow.AddMinutes(5));

        try
        {
            await _sender.SendAsync(phone, $"رمز التحقق: {code}", ct);
        }
        catch (Exception ex)
        {
            _codes.TryRemove(challengeId, out _);
            _logger.LogError(ex, "[SmsTFA] Failed to send code to {Phone}", phone);
            return new ChallengeResult(false, "", "send_failed");
        }

        _logger.LogInformation("[SmsTFA] Challenge {Id} created for {Phone}", challengeId, phone);

        return new ChallengeResult(
            Succeeded: true,
            ChallengeId: challengeId,
            ProviderData: new Dictionary<string, string>
            {
                ["phone"] = MaskPhone(phone)
            });
    }

    public Task<VerificationResult> VerifyAsync(
        string challengeId,
        string? providedCode = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providedCode))
            return Task.FromResult(new VerificationResult(false, "code_required"));

        if (!_codes.TryGetValue(challengeId, out var pending))
            return Task.FromResult(new VerificationResult(false, "challenge_not_found"));

        if (pending.ExpiresAt < DateTimeOffset.UtcNow)
        {
            _codes.TryRemove(challengeId, out _);
            return Task.FromResult(new VerificationResult(false, "expired"));
        }

        // مقارنة آمنة ضد timing attacks
        if (!ConstantTimeEquals(pending.Code, providedCode))
            return Task.FromResult(new VerificationResult(false, "wrong_code"));

        // نجح - نحذف الكود
        _codes.TryRemove(challengeId, out _);
        _logger.LogInformation("[SmsTFA] Challenge {Id} verified", challengeId);

        return Task.FromResult(new VerificationResult(true));
    }

    private static string GenerateCode()
    {
        // 6 أرقام عشوائية مشفرة (cryptographically secure)
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);
        var num = BitConverter.ToUInt32(bytes, 0) % 1_000_000;
        return num.ToString("D6");
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }

    private static string MaskPhone(string phone) =>
        phone.Length > 4 ? $"****{phone[^4..]}" : "****";

    private record PendingCode(string Code, string Phone, DateTimeOffset ExpiresAt);
}
