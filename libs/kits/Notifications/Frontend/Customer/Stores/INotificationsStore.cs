namespace ACommerce.Kits.Notifications.Frontend.Customer.Stores;

/// <summary>
/// store reactive لـ inbox الإشعارات على العميل. يَعرض
/// <see cref="NotificationItem"/> POCO بسيط — التطبيق يَربط store يَجلب من
/// <c>/notifications</c> ويُحدِّث realtime عبر OAM <c>notification.received</c>.
/// </summary>
public interface INotificationsStore
{
    IReadOnlyList<NotificationItem> Items { get; }
    int UnreadCount { get; }
    bool IsLoading { get; }
    event Action? Changed;

    Task LoadAsync(CancellationToken ct = default);
    Task MarkReadAsync(string id, CancellationToken ct = default);
    Task MarkAllReadAsync(CancellationToken ct = default);
}

/// <summary>عنصر إشعار للعرض. لا HTML خام — Body نصّ عاديّ يُمرَّر عبر sanitizer.</summary>
public sealed record NotificationItem(
    string Id,
    string Title,
    string Body,
    string? Kind,
    string? DeepLinkUrl,
    DateTime CreatedAt,
    bool IsRead);
