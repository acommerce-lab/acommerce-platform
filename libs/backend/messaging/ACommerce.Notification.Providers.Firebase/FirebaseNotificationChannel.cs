using ACommerce.Notification.Operations.Abstractions;
using ACommerce.Notification.Providers.Firebase.Options;
using ACommerce.Notification.Providers.Firebase.Storage;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;

namespace ACommerce.Notification.Providers.Firebase;

/// <summary>
/// قناة إشعارات Firebase Cloud Messaging (Push).
///
/// تحل رموز الأجهزة عبر <see cref="IDeviceTokenStore"/> ثم ترسل عبر FirebaseAdmin SDK.
/// تدعم الأجهزة المتعددة لكل مستخدم وتُزيل الرموز غير الصالحة تلقائياً.
/// </summary>
public class FirebaseNotificationChannel : INotificationChannel
{
    private readonly IDeviceTokenStore _tokenStore;
    private readonly FirebaseOptions _options;
    private readonly ILogger<FirebaseNotificationChannel> _logger;
    private readonly FirebaseMessaging _messaging;

    public string ChannelName => "firebase";

    public FirebaseNotificationChannel(
        IDeviceTokenStore tokenStore,
        FirebaseOptions options,
        ILogger<FirebaseNotificationChannel> logger)
    {
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _messaging = InitializeMessaging();
    }

    public async Task<bool> SendAsync(
        string userId,
        string title,
        string message,
        object? data = null,
        CancellationToken ct = default)
    {
        var tokens = await _tokenStore.GetTokensAsync(userId, ct);

        if (tokens.Count == 0)
        {
            _logger.LogDebug("[Firebase] No device tokens for user {UserId}", userId);
            return false;
        }

        try
        {
            var dataDict = ConvertToStringDict(data);

            var notification = new FirebaseAdmin.Messaging.Notification
            {
                Title = title,
                Body = message
            };

            var android = new AndroidConfig
            {
                Notification = new AndroidNotification
                {
                    Sound = _options.DefaultSound,
                    Icon = _options.DefaultIcon,
                    Color = _options.DefaultColor,
                    ClickAction = _options.DefaultClickAction
                }
            };

            var apns = new ApnsConfig
            {
                Aps = new Aps
                {
                    Sound = _options.DefaultSound,
                    Alert = new ApsAlert { Title = title, Body = message }
                }
            };

            // إرسال متعدد إذا كان هناك أكثر من جهاز
            if (tokens.Count > 1)
            {
                var multicast = new MulticastMessage
                {
                    Tokens = tokens.ToList(),
                    Notification = notification,
                    Data = dataDict,
                    Android = android,
                    Apns = apns
                };

                var response = await _messaging.SendEachForMulticastAsync(multicast, ct);

                _logger.LogInformation(
                    "[Firebase] Sent to {UserId}: {Success}/{Total} succeeded",
                    userId, response.SuccessCount, tokens.Count);

                if (_options.RemoveInvalidTokens && response.FailureCount > 0)
                    await CleanupInvalidTokensAsync(tokens.ToList(), response.Responses, ct);

                return response.SuccessCount > 0;
            }
            else
            {
                var single = new Message
                {
                    Token = tokens[0],
                    Notification = notification,
                    Data = dataDict,
                    Android = android,
                    Apns = apns
                };

                try
                {
                    var msgId = await _messaging.SendAsync(single, ct);
                    _logger.LogInformation("[Firebase] Sent to {UserId}, msgId: {MsgId}", userId, msgId);
                    return true;
                }
                catch (FirebaseMessagingException fex) when (IsInvalidTokenError(fex))
                {
                    if (_options.RemoveInvalidTokens)
                    {
                        await _tokenStore.UnregisterAsync(tokens[0], ct);
                        _logger.LogInformation("[Firebase] Removed invalid token for {UserId}", userId);
                    }
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Firebase] Failed to send to user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> ValidateAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        var tokens = await _tokenStore.GetTokensAsync(userId, ct);
        return tokens.Count > 0;
    }

    // =============================================
    // Private Helpers
    // =============================================

    private FirebaseMessaging InitializeMessaging()
    {
        // إعادة استخدام التطبيق الافتراضي إن وُجد
        var app = FirebaseApp.DefaultInstance;

        if (app == null)
        {
            GoogleCredential credential;

            if (!string.IsNullOrEmpty(_options.CredentialsJson))
            {
                credential = GoogleCredential.FromJson(_options.CredentialsJson);
            }
            else if (!string.IsNullOrEmpty(_options.CredentialsFilePath))
            {
                credential = GoogleCredential.FromFile(_options.CredentialsFilePath);
            }
            else
            {
                throw new InvalidOperationException(
                    "Firebase credentials not configured. Set CredentialsFilePath or CredentialsJson.");
            }

            var appOptions = new AppOptions
            {
                Credential = credential,
                ProjectId = _options.ProjectId
            };

            app = FirebaseApp.Create(appOptions);
        }

        return FirebaseMessaging.GetMessaging(app);
    }

    private static Dictionary<string, string>? ConvertToStringDict(object? data)
    {
        if (data == null) return null;

        if (data is IDictionary<string, string> stringDict)
            return new Dictionary<string, string>(stringDict);

        if (data is IDictionary<string, object> objDict)
        {
            return objDict.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.ToString() ?? string.Empty);
        }

        // فحص الخصائص (POCO)
        var props = data.GetType().GetProperties();
        var result = new Dictionary<string, string>(props.Length);
        foreach (var prop in props)
        {
            var value = prop.GetValue(data)?.ToString();
            if (value != null) result[prop.Name] = value;
        }
        return result.Count > 0 ? result : null;
    }

    private static bool IsInvalidTokenError(FirebaseMessagingException ex) =>
        ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
        ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument ||
        ex.MessagingErrorCode == MessagingErrorCode.SenderIdMismatch;

    private async Task CleanupInvalidTokensAsync(
        List<string> tokens,
        IReadOnlyList<SendResponse> responses,
        CancellationToken ct)
    {
        for (int i = 0; i < responses.Count; i++)
        {
            if (!responses[i].IsSuccess && responses[i].Exception is FirebaseMessagingException fex && IsInvalidTokenError(fex))
            {
                await _tokenStore.UnregisterAsync(tokens[i], ct);
                _logger.LogInformation("[Firebase] Removed invalid token: {Token}", MaskToken(tokens[i]));
            }
        }
    }

    private static string MaskToken(string token) =>
        token.Length > 8 ? $"{token[..4]}...{token[^4..]}" : "****";
}
