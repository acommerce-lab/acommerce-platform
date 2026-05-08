using ACommerce.Compositions.Customer.Unread;
using ACommerce.Kits.Chat.Frontend.Customer.Stores;
using ACommerce.Kits.Notifications.Frontend.Customer.Stores;

namespace Ejar.Customer.UI.Services;

/// <summary>
/// V1 façade فوق <see cref="UnreadComposition"/> + kit Stores (F63).
/// الصَفحات تَحتَفِظ بِنَفس API (<c>ChatUnread</c>، <c>NotifUnread</c>،
/// <c>RefreshAsync</c>، <c>ActiveConversationId</c>) لكن العَمَل الفِعليّ
/// يَنتَقِل لِطَبَقَة OAM:
/// <list type="bullet">
///   <item>القِراءة → <c>UnreadComposition</c> (مُشتَقّة مِن
///         <c>IChatStore.UnreadTotal</c> + <c>INotificationsStore.UnreadCount</c>)</item>
///   <item>الكِتابة → no-op (القِيَم مُشتَقَّة)</item>
///   <item>RefreshAsync → يَستَدعي LoadConversationsAsync + LoadAsync لِلكيتس</item>
///   <item>ClearChat/ClearNotif → no-op (تَأتي عِند LoadAsync)</item>
/// </list>
/// </summary>
public sealed class UnreadService : IDisposable
{
    private readonly UnreadComposition _composition;
    private readonly IChatStore _chat;
    private readonly INotificationsStore _notif;

    public UnreadService(UnreadComposition composition, IChatStore chat, INotificationsStore notif)
    {
        _composition = composition;
        _chat = chat;
        _notif = notif;
        _composition.Changed += OnCompositionChanged;
    }

    public int ChatUnread  { get => _composition.ChatUnread;  set { /* legacy setter — derived */ } }
    public int NotifUnread { get => _composition.NotifUnread; set { /* legacy setter — derived */ } }
    public string? ActiveConversationId { get; set; }

    public event Action? Changed;
    public void RaiseChanged() => Changed?.Invoke();

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await Task.WhenAll(
            _chat.LoadConversationsAsync(ct),
            _notif.LoadAsync(ct));
    }

    public void ClearChat()  { /* derived from store; no-op */ }
    public void ClearNotif() { /* derived from store; no-op */ }

    private void OnCompositionChanged() => Changed?.Invoke();

    public void Dispose() => _composition.Changed -= OnCompositionChanged;
}
