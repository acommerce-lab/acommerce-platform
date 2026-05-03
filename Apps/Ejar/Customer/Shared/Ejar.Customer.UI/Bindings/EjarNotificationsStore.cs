using ACommerce.Kits.Notifications.Frontend.Customer.Stores;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="INotificationsStore"/> لإيجار. يَجلب من
/// <c>GET /notifications</c> ويَدفع mark-read عبر
/// <c>POST /notifications/{id}/read</c> + <c>POST /notifications/read-all</c>.
/// </summary>
public sealed class EjarNotificationsStore : INotificationsStore
{
    private readonly ApiReader _api;
    private List<NotificationItem> _items = new();

    public EjarNotificationsStore(ApiReader api) => _api = api;

    public IReadOnlyList<NotificationItem> Items => _items;
    public int UnreadCount => _items.Count(i => !i.IsRead);
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _api.GetAsync<List<NotificationItem>>("/notifications", ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                _items = env.Data;
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task MarkReadAsync(string id, CancellationToken ct = default)
    {
        var env = await _api.PostAsync<object>($"/notifications/{Uri.EscapeDataString(id)}/read", null, ct);
        if (env.Operation.Status != "Success") return;
        _items = _items.Select(i => i.Id == id ? i with { IsRead = true } : i).ToList();
        Changed?.Invoke();
    }

    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        var env = await _api.PostAsync<object>("/notifications/read-all", null, ct);
        if (env.Operation.Status != "Success") return;
        _items = _items.Select(i => i with { IsRead = true }).ToList();
        Changed?.Invoke();
    }
}
