using System.Text.Json;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Services;

/// <summary>
/// عدّاد موحَّد للرسائل والإشعارات غير المقروءة. يُحقَن في MainLayout
/// ويُغذِّي الشارات الحمراء على أيقونات Bottom/Top nav.
///
/// <para>المصادر:
///   <list type="bullet">
///     <item><c>GET /conversations</c> — مَجموع <c>UnreadCount</c> عبر كلّ المحادثات.</item>
///     <item><c>GET /notifications/unread-count</c> — لو موجود، وإلّا fallback لـ <c>GET /notifications</c>.</item>
///     <item><c>EjarRealtimeService.MessageReceived</c> — يَزيد ChatUnread.</item>
///     <item><c>EjarRealtimeService.NotificationReceived</c> — يَزيد NotifUnread.</item>
///   </list>
/// </para>
///
/// <para>متى يُمسَح:
///   <list type="bullet">
///     <item>عند فتح صفحة /chats أو /chat/{id} ⇒ ChatUnread = 0 لتلك المحادثة.</item>
///     <item>عند فتح /notifications أو ضغط "تحديد كلّها كمقروءة" ⇒ NotifUnread = 0.</item>
///   </list>
/// </para>
/// </summary>
public sealed class UnreadService : IDisposable
{
    private readonly AppStore _store;
    private readonly ApiReader _api;
    private readonly EjarRealtimeService _realtime;

    public int ChatUnread  { get; set; }
    public int NotifUnread { get; set; }

    public event Action? Changed;

    /// <summary>أَطلق Changed بَعْد set مباشر للقيمة من المُستهلِكين.</summary>
    public void RaiseChanged() => Changed?.Invoke();

    public UnreadService(AppStore store, ApiReader api, EjarRealtimeService realtime)
    {
        _store = store;
        _api = api;
        _realtime = realtime;
        _realtime.MessageReceived      += OnMessage;
        _realtime.NotificationReceived += OnNotification;
    }

    /// <summary>يَجلب الـ counts من الـ API. يُستدعى من MainLayout بعد المصادقة.</summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (!_store.Auth.IsAuthenticated) return;
        await Task.WhenAll(RefreshChatAsync(ct), RefreshNotifAsync(ct));
    }

    private async Task RefreshChatAsync(CancellationToken ct)
    {
        try
        {
            var env = await _api.GetAsync<List<ConvUnreadDto>>("/conversations", ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
            {
                ChatUnread = env.Data.Sum(c => c.UnreadCount);
                Changed?.Invoke();
            }
        }
        catch { /* network/shape — keep last */ }
    }

    private async Task RefreshNotifAsync(CancellationToken ct)
    {
        try
        {
            var env = await _api.GetAsync<UnreadCountDto>("/notifications/unread-count", ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
            {
                NotifUnread = env.Data.Count;
                Changed?.Invoke();
                return;
            }
        }
        catch { /* endpoint might not exist — fall back */ }

        try
        {
            var env = await _api.GetAsync<List<NotifFlagDto>>("/notifications", ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
            {
                NotifUnread = env.Data.Count(n => !n.IsRead);
                Changed?.Invoke();
            }
        }
        catch { /* keep last */ }
    }

    /// <summary>
    /// id المحادثة المفتوحة حاليّاً. الرسائل التي تَرد لها لا تُحسَب
    /// كَغير-مَقروءة (المُستخدِم يَراها لحظيّاً). ChatRoom.razor يَضبطها
    /// عند الـ Enter ويَمسحها عند الـ Leave.
    /// </summary>
    public string? ActiveConversationId { get; set; }

    private void OnMessage(string json)
    {
        // ١. لا تَزِد عدّاد على رسالة من المستخدِم نفسه (server echo).
        // ٢. لا تَزِد على رسالة من المحادثة المفتوحة (المُستخدِم يَراها).
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var sender = TryGet(root, "senderPartyId") ?? TryGet(root, "SenderPartyId");
            var convId = TryGet(root, "conversationId") ?? TryGet(root, "ConversationId");

            var myPartyId = _store.Auth.UserId is { } g ? $"User:{g}" : null;
            if (!string.IsNullOrEmpty(sender) && sender == myPartyId) return;
            if (!string.IsNullOrEmpty(convId) && convId == ActiveConversationId) return;
        }
        catch { /* parsing failed — treat as new */ }

        ChatUnread++;
        Changed?.Invoke();
    }

    private void OnNotification(string _)
    {
        NotifUnread++;
        Changed?.Invoke();
    }

    private static string? TryGet(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
           ? p.GetString() : null;

    public void ClearChat()  { ChatUnread = 0;  Changed?.Invoke(); }
    public void ClearNotif() { NotifUnread = 0; Changed?.Invoke(); }

    public void Dispose()
    {
        _realtime.MessageReceived      -= OnMessage;
        _realtime.NotificationReceived -= OnNotification;
    }

    private sealed record ConvUnreadDto(string Id, int UnreadCount);
    private sealed record UnreadCountDto(int Count);
    private sealed record NotifFlagDto(string Id, bool IsRead);
}
