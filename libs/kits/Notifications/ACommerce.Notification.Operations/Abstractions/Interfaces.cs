using ACommerce.OperationEngine.Core;
namespace ACommerce.Notification.Operations.Abstractions;

/// <summary>
/// واجهة الإشعار - لا كيان!
/// </summary>
public interface INotification
{
    Guid Id { get; }
    string UserId { get; }
    string Title { get; }
    string Message { get; }
    string NotificationType { get; }
    DateTime CreatedAt { get; }
}

/// <summary>
/// واجهة قناة الإشعار. المطور يُطبقها: InApp, Push, Email, SMS...
/// </summary>
public interface INotificationChannel
{
    string ChannelName { get; }
    Task<bool> SendAsync(string userId, string title, string message, object? data = null, CancellationToken ct = default);
    Task<bool> ValidateAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// مفاتيح علامات الإشعارات. المفاتيح ثابتة، القيم حرة.
/// </summary>
public static class NotifyTags
{
    /// <summary>
    /// القناة. القيم: "inapp", "push", "email", "sms", "whatsapp"
    /// </summary>
    public static readonly TagKey Channel = new("channel");

    /// <summary>
    /// نوع الإشعار. القيم من التطبيق: "new_order", "message", "payment", "system"
    /// </summary>
    public static readonly TagKey NotificationType = new("notification_type");

    /// <summary>
    /// الأولوية. القيم: "low", "normal", "high", "urgent"
    /// </summary>
    public static readonly TagKey Priority = new("priority");

    /// <summary>
    /// حالة التسليم لكل قناة. القيم: "pending", "sent", "failed"
    /// </summary>
    public static readonly TagKey ChannelDelivery = new("channel_delivery");
}
