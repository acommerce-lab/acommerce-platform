using ACommerce.Notification.Operations.Abstractions;
using ACommerce.Realtime.Operations.Abstractions;
using Microsoft.Extensions.Logging;

namespace ACommerce.Notification.Providers.InApp;

/// <summary>
/// قناة إشعارات داخل التطبيق - مبنية على تجريد <see cref="IRealtimeTransport"/>.
///
/// لا تعرف ما إذا كان النقل SignalR أو WebSocket أو gRPC أو InMemory.
/// المطور يُسجل أي مزود لـ IRealtimeTransport في DI، وهذه القناة تستخدمه.
///
/// الاستخدام:
///   services.AddSignalRRealtimeTransport&lt;MyHub, IMyClient&gt;();  // أو InMemory أو غيره
///   services.AddInAppNotificationChannel();
/// </summary>
public class InAppNotificationChannel : INotificationChannel
{
    private readonly IRealtimeTransport _transport;
    private readonly IConnectionTracker? _tracker;
    private readonly ILogger<InAppNotificationChannel> _logger;
    private readonly InAppOptions _options;

    public string ChannelName => "inapp";

    public InAppNotificationChannel(
        IRealtimeTransport transport,
        ILogger<InAppNotificationChannel> logger,
        InAppOptions? options = null,
        IConnectionTracker? tracker = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new InAppOptions();
        _tracker = tracker;
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
            _logger.LogWarning("[InApp] Cannot send - userId is empty");
            return false;
        }

        try
        {
            var payload = new InAppPayload(
                Title: title,
                Message: message,
                Data: data,
                Timestamp: DateTimeOffset.UtcNow);

            await _transport.SendToUserAsync(userId, _options.MethodName, payload, ct);
            _logger.LogDebug("[InApp] Sent '{Title}' to user {UserId}", title, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[InApp] Failed to send notification to user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> ValidateAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;

        // إذا كان OfflineDeliveryAllowed = true، لا يحتاج المستخدم اتصالاً نشطاً
        // النقل قد يحفظ الرسالة لتسليمها لاحقاً
        if (_options.AllowOffline) return true;

        // وإلا، نتحقق من وجود اتصال نشط
        if (_tracker == null) return true;

        return await _tracker.IsOnlineAsync(userId, ct);
    }
}

/// <summary>
/// إعدادات قناة الإشعارات داخل التطبيق.
/// </summary>
public class InAppOptions
{
    /// <summary>
    /// اسم الدالة المُستدعاة على العميل.
    /// مثال: "ReceiveNotification" - يجب أن يطابقها العميل.
    /// </summary>
    public string MethodName { get; set; } = "ReceiveNotification";

    /// <summary>
    /// السماح بالتسليم للمستخدمين غير المتصلين.
    /// إذا = true: ValidateAsync ترجع true دائماً (لا تتحقق من الاتصال).
    /// إذا = false: تتحقق من وجود اتصال نشط عبر IConnectionTracker.
    /// </summary>
    public bool AllowOffline { get; set; } = true;
}

/// <summary>
/// حمولة الإشعار داخل التطبيق المُرسلة عبر النقل.
/// </summary>
public record InAppPayload(
    string Title,
    string Message,
    object? Data,
    DateTimeOffset Timestamp);
