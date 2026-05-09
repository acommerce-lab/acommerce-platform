using ACommerce.Notification.Operations.Abstractions;
using ACommerce.Notification.Providers.Email.Options;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace ACommerce.Notification.Providers.Email;

/// <summary>Email notification channel via SMTP.</summary>
public class EmailNotificationChannel : INotificationChannel
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailNotificationChannel> _logger;

    public string ChannelName => "email";

    public EmailNotificationChannel(EmailOptions options, ILogger<EmailNotificationChannel> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> SendAsync(
        string userId,
        string title,
        string message,
        object? data = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("[Email] Cannot send - userId/email is empty");
            return false;
        }

        try
        {
            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.UseSsl,
                Credentials = new NetworkCredential(_options.Username, _options.Password)
            };

            var from = new MailAddress(_options.FromAddress, _options.FromName);
            var to = new MailAddress(userId);

            using var mail = new MailMessage(from, to)
            {
                Subject = title,
                Body = message,
                IsBodyHtml = false
            };

            await client.SendMailAsync(mail, ct);
            _logger.LogInformation("[Email] Sent '{Title}' to {UserId}", title, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Email] Failed to send notification to {UserId}", userId);
            return false;
        }
    }

    public Task<bool> ValidateAsync(string userId, CancellationToken ct = default)
    {
        var isValid = !string.IsNullOrWhiteSpace(userId) && userId.Contains('@');
        return Task.FromResult(isValid);
    }
}
