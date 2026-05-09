using ACommerce.ClientHost.KitApi;

namespace ACommerce.Kits.Notifications.Frontend.Customer.Stores;

/// <summary>
/// تنفيذ افتراضيّ يَستهلك <see cref="KitHttpClient"/> الموحَّد ⇒ يَستفيد
/// تلقائيّاً من analyzers/interceptors المُسَجَّلة (auth، telemetry، ...).
/// يَعرف الـ wire shapes فقط:
/// <list type="bullet">
///   <item><c>GET /notifications</c> ⇒ <c>List&lt;NotificationItem&gt;</c></item>
///   <item><c>POST /notifications/{id}/read</c> ⇒ <c>{ id }</c></item>
///   <item><c>POST /notifications/read-all</c> ⇒ <c>{ count }</c></item>
/// </list>
/// </summary>
public sealed class HttpNotificationsApiClient : INotificationsApiClient
{
    private const string Kit = "notifications";
    private readonly KitHttpClient _http;

    public HttpNotificationsApiClient(KitHttpClient http) => _http = http;

    public async Task<IReadOnlyList<NotificationItem>> ListAsync(CancellationToken ct = default)
    {
        var res = await _http.GetAsync<List<NotificationItem>>(Kit, "/notifications", ct);
        return res.Success && res.Data is not null ? res.Data : Array.Empty<NotificationItem>();
    }

    public async Task<bool> MarkReadAsync(string id, CancellationToken ct = default)
    {
        var res = await _http.PostAsync<object>(Kit, $"/notifications/{Uri.EscapeDataString(id)}/read", null, ct);
        return res.Success;
    }

    public async Task<bool> MarkAllReadAsync(CancellationToken ct = default)
    {
        var res = await _http.PostAsync<object>(Kit, "/notifications/read-all", null, ct);
        return res.Success;
    }
}
