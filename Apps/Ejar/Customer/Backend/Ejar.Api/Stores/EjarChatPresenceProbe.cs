using ACommerce.Kits.Chat.Backend;
using ACommerce.Realtime.Operations.Abstractions;

namespace Ejar.Api.Stores;

/// <summary>
/// تنفيذ <see cref="IPresenceProbe"/> عبر <see cref="IRealtimeChannelManager"/>.
/// المستخدم "حاضر" في المحادثة لو قناة <c>chat:conv:{id}</c> مفتوحة له
/// (أي استدعى /chat/{conv}/enter). الـ enter يفتحها، الـ leave/idle يُغلقها.
///
/// <para>هذا القرار سياسة تطبيق — يعيش هنا في Ejar.Api لا في kits.</para>
/// </summary>
public sealed class EjarChatPresenceProbe : IPresenceProbe
{
    private readonly IRealtimeChannelManager? _channels;

    public EjarChatPresenceProbe(IRealtimeChannelManager? channels = null)
    {
        _channels = channels;
    }

    public Task<bool> IsUserActiveInConversationAsync(
        string userId, string conversationId, CancellationToken ct = default)
    {
        if (_channels is null) return Task.FromResult(false);
        // Chat channel id format: نفس ChatChannels.Chat(convId) في الـ kit.
        // إعادة بناء بسيطة حتى لا نحقن ChatService هنا.
        var chatChannelId = $"chat:conv:{conversationId}";
        return Task.FromResult(_channels.IsOpen(userId, chatChannelId));
    }
}
