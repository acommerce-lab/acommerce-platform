using ACommerce.Chat.Operations;

namespace ACommerce.Kits.Chat.Frontend.Customer.Stores;

/// <summary>
/// عميل HTTP خاصّ بـ Chat kit. الإرسال + استقبال الرسائل realtime يَتمّ عبر
/// <c>IChatClient</c> (SignalR/hub). هذا العميل خاصّ بـ REST endpoints:
/// <list type="bullet">
///   <item><c>GET /conversations</c> ⇒ <c>ConversationSummary[]</c></item>
///   <item><c>GET /conversations/{id}/messages</c> ⇒ <c>IChatMessage[]</c></item>
///   <item><c>POST /chat/{id}/enter</c> — يُعَلّم القراءة + presence.</item>
/// </list>
/// </summary>
public interface IChatApiClient
{
    Task<IReadOnlyList<ConversationSummary>>  ListConversationsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IChatMessage>>         ListMessagesAsync(string conversationId, CancellationToken ct = default);
    Task<bool>                                EnterAsync(string conversationId, CancellationToken ct = default);
}
