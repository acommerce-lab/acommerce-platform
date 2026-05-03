using ACommerce.Kits.Notifications.Frontend.Customer.Stores;

namespace Ejar.Customer.UI.Bindings;

public sealed class EjarNotificationsStore : INotificationsStore
{
    public IReadOnlyList<NotificationItem> Items { get; private set; } = Array.Empty<NotificationItem>();
    public int UnreadCount => Items.Count(i => !i.IsRead);
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public Task LoadAsync(CancellationToken ct = default)               { Changed?.Invoke(); return Task.CompletedTask; }
    public Task MarkReadAsync(string id, CancellationToken ct = default){ Changed?.Invoke(); return Task.CompletedTask; }
    public Task MarkAllReadAsync(CancellationToken ct = default)        { Changed?.Invoke(); return Task.CompletedTask; }
}
