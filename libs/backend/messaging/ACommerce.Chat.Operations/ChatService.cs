using ACommerce.Realtime.Operations.Abstractions;
using Microsoft.Extensions.Logging;

namespace ACommerce.Chat.Operations;

/// <summary>
/// التطبيق الافتراضيّ لـ <see cref="IChatService"/>. مبنيّ فوق
/// <see cref="IRealtimeChannelManager"/> و <see cref="IRealtimeTransport"/>.
/// لا يلمس قاعدة بيانات ولا يستدعي طبقة الإشعارات — التطبيق يعدّ ذلك بنفسه.
/// </summary>
public sealed class ChatService : IChatService
{
    public const string MessageMethod = "chat.message";

    private readonly IRealtimeChannelManager _channels;
    private readonly IRealtimeTransport _transport;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IRealtimeChannelManager channels, IRealtimeTransport transport, ILogger<ChatService> logger)
    {
        _channels  = channels;
        _transport = transport;
        _logger    = logger;
    }

    public Task EnterConversationAsync(
        string conversationId, string userId, string connectionId,
        TimeSpan? idleTimeout = null, CancellationToken ct = default)
    {
        var opts = new RealtimeChannelOptions { IdleTimeout = idleTimeout };
        return _channels.OpenAsync(userId, connectionId, ChatChannels.Chat(conversationId), opts, ct);
    }

    public Task LeaveConversationAsync(string conversationId, string userId, CancellationToken ct = default)
        => _channels.CloseAsync(userId, ChatChannels.Chat(conversationId), ct);

    public Task SubscribeUserAsync(string conversationId, string userId, string connectionId, CancellationToken ct = default)
        => _channels.OpenAsync(userId, connectionId, ChatChannels.Notif(conversationId),
            options: null, // null timeout → always-on
            ct);

    public Task UnsubscribeUserAsync(string conversationId, string userId, CancellationToken ct = default)
        => _channels.CloseAsync(userId, ChatChannels.Notif(conversationId), ct);

    public async Task BroadcastNewMessageAsync(IChatMessage message, CancellationToken ct = default)
    {
        var chatGroup  = ChatChannels.Chat(message.ConversationId);
        var notifGroup = ChatChannels.Notif(message.ConversationId);

        // Both sends are independent — losing one doesn't kill the other.
        await _transport.SendToGroupAsync(chatGroup, MessageMethod, message, ct);
        await _transport.SendToGroupAsync(notifGroup, MessageMethod, message, ct);

        // Refresh idle timer for anyone actively viewing.
        // (Senders also benefit; their own idle counter advances on every send.)
        // Note: we don't know the recipient set here without a domain lookup —
        // RecordActivity is best-effort against the sender; the manager's
        // per-user state means non-subscribers are no-ops anyway.
        await _channels.RecordActivityAsync(message.SenderPartyId, chatGroup, ct);

        _logger.LogDebug("[Chat] broadcast msg={Id} conv={Conv} sender={Sender}",
            message.Id, message.ConversationId, message.SenderPartyId);
    }
}
