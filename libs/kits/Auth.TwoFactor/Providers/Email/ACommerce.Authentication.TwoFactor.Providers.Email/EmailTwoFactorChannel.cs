using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ACommerce.Authentication.TwoFactor.Providers.Email;

/// <summary>
/// واجهة مُرسل البريد - المطور يُطبقها.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

/// <summary>
/// مُرسل بريد وهمي - يسجل الرسالة في الـ logs للتطوير.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogWarning("═══════════════════════════════════════════════");
        _logger.LogWarning("📧 [DUMMY EMAIL] to: {To}", to);
        _logger.LogWarning("📧 [DUMMY EMAIL] subject: {Subject}", subject);
        _logger.LogWarning("📧 [DUMMY EMAIL] body: {Body}", body);
        _logger.LogWarning("═══════════════════════════════════════════════");
        return Task.CompletedTask;
    }
}

/// <summary>
/// إعدادات قناة Email 2FA.
/// </summary>
public class EmailTwoFactorOptions
{
    public string Subject { get; set; } = "رمز التحقق";
    public string BodyTemplate { get; set; } = "رمز التحقق الخاص بك هو: {0}\nصالح لمدة 10 دقائق.";
    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public int CodeLength { get; set; } = 6;
}

/// <summary>
/// قناة 2FA عبر البريد الإلكتروني.
/// تُولّد كود عشوائي، ترسله لعنوان البريد، وتتحقق من التطابق محلياً.
/// </summary>
public class EmailTwoFactorChannel : ITwoFactorChannel
{
    private readonly IEmailSender _sender;
    private readonly EmailTwoFactorOptions _options;
    private readonly ILogger<EmailTwoFactorChannel> _logger;

    private readonly ConcurrentDictionary<string, PendingEmailCode> _codes = new();

    public string Name => "email";
    public bool GeneratesCode => true;

    public EmailTwoFactorChannel(
        IEmailSender sender,
        EmailTwoFactorOptions options,
        ILogger<EmailTwoFactorChannel> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChallengeResult> InitiateAsync(
        string userIdentifier,
        string? target = null,
        CancellationToken ct = default)
    {
        var email = target ?? userIdentifier;
        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
            return new ChallengeResult(false, "", "invalid_email");

        var code = GenerateCode(_options.CodeLength);
        var challengeId = Guid.NewGuid().ToString("N");

        _codes[challengeId] = new PendingEmailCode(
            code, email, DateTimeOffset.UtcNow.Add(_options.CodeLifetime));

        try
        {
            var body = string.Format(_options.BodyTemplate, code);
            await _sender.SendAsync(email, _options.Subject, body, ct);
        }
        catch (Exception ex)
        {
            _codes.TryRemove(challengeId, out _);
            _logger.LogError(ex, "[EmailTFA] Failed to send code to {Email}", email);
            return new ChallengeResult(false, "", "send_failed");
        }

        _logger.LogInformation("[EmailTFA] Challenge {Id} created for {Email}", challengeId, email);

        return new ChallengeResult(
            Succeeded: true,
            ChallengeId: challengeId,
            ProviderData: new Dictionary<string, string>
            {
                ["email"] = MaskEmail(email)
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

        if (!ConstantTimeEquals(pending.Code, providedCode))
            return Task.FromResult(new VerificationResult(false, "wrong_code"));

        _codes.TryRemove(challengeId, out _);
        _logger.LogInformation("[EmailTFA] Challenge {Id} verified", challengeId);

        return Task.FromResult(new VerificationResult(true));
    }

    private static string GenerateCode(int length)
    {
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);
        var max = (uint)Math.Pow(10, length);
        var num = BitConverter.ToUInt32(bytes, 0) % max;
        return num.ToString(new string('0', length));
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }

    private static bool IsValidEmail(string email)
    {
        try { return new System.Net.Mail.MailAddress(email).Address == email; }
        catch { return false; }
    }

    private static string MaskEmail(string email)
    {
        var atIdx = email.IndexOf('@');
        if (atIdx < 2) return "****";
        return $"{email[0]}***{email[atIdx..]}";
    }

    private record PendingEmailCode(string Code, string Email, DateTimeOffset ExpiresAt);
}
