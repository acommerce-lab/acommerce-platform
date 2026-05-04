using ACommerce.Kits.Notifications.Frontend.Customer.Stores;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="INotificationsStore"/> لإيجار. يَدلّع للـ
/// <see cref="INotificationsApiClient"/> الذي يَملك الكيت — هو من يَعرف
/// شكل الـ envelope. الـ store يُدير state الـ UI فقط (loading، list،
/// Changed event) — لا shape knowledge هنا.
/// </summary>
public sealed class EjarNotificationsStore : INotificationsStore
{
    private readonly INotificationsApiClient _api;
    private List<NotificationItem> _items = new();

    public EjarNotificationsStore(INotificationsApiClient api) => _api = api;

    public IReadOnlyList<NotificationItem> Items => _items;
    public int UnreadCount => _items.Count(i => !i.IsRead);
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try   { _items = (await _api.ListAsync(ct)).ToList(); }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task MarkReadAsync(string id, CancellationToken ct = default)
    {
        if (!await _api.MarkReadAsync(id, ct)) return;
        _items = _items.Select(i => i.Id == id ? i with { IsRead = true } : i).ToList();
        Changed?.Invoke();
    }

    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        if (!await _api.MarkAllReadAsync(ct)) return;
        _items = _items.Select(i => i with { IsRead = true }).ToList();
        Changed?.Invoke();
    }
}
