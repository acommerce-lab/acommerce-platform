using ACommerce.Client.Operations;

namespace ACommerce.Kits.Notifications.Frontend.Customer.Stores;

/// <summary>OAM-shaped (F61) — يَدفَع notifications.* operations عَبر ITemplateEngine.</summary>
public sealed class DefaultNotificationsStore : INotificationsStore
{
    private readonly ITemplateEngine _engine;
    private List<NotificationItem> _items = new();

    public DefaultNotificationsStore(ITemplateEngine engine) => _engine = engine;

    public IReadOnlyList<NotificationItem> Items => _items;
    public int UnreadCount => _items.Count(i => !i.IsRead);
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _engine.ExecuteAsync<List<NotificationItem>>(NotificationsOps.List(), ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                _items = env.Data;
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task MarkReadAsync(string id, CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<object>(NotificationsOps.MarkRead(id), ct: ct);
        if (env.Operation.Status != "Success") return;
        _items = _items.Select(i => i.Id == id ? i with { IsRead = true } : i).ToList();
        Changed?.Invoke();
    }

    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<object>(NotificationsOps.MarkAllRead(), ct: ct);
        if (env.Operation.Status != "Success") return;
        _items = _items.Select(i => i with { IsRead = true }).ToList();
        Changed?.Invoke();
    }

    /// <summary>مَدخَل realtime: composition تَدفَع إشعاراً وارِداً مِن الـ hub.</summary>
    public void IngestRealtimeNotification(NotificationItem n)
    {
        _items = _items.Prepend(n).ToList();
        Changed?.Invoke();
    }
}
