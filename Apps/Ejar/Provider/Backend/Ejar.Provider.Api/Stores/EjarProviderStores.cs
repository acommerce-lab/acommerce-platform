using ACommerce.Chat.Operations;
using ACommerce.Kits.Auth.Backend;
using ACommerce.Kits.Chat.Backend;
using Ejar.Domain;

namespace Ejar.Provider.Api.Stores;

/// <summary>
/// Bridge between the Auth Kit and EjarSeed. The whole "look up phone, create
/// user if missing, return display name" surface area is two methods.
/// </summary>
public sealed class EjarProviderAuthUserStore : IAuthUserStore
{
    public Task<string> GetOrCreateUserIdAsync(string phone, CancellationToken ct)
        => Task.FromResult(EjarSeed.GetOrCreateUserId(phone));

    public Task<string?> GetDisplayNameAsync(string userId, CancellationToken ct)
        => Task.FromResult(EjarSeed.GetUser(userId)?.FullName);
}

/// <summary>
/// Bridge between the Chat Kit and EjarSeed for Provider-side participation.
/// Provider sees only conversations where it is the partner. Mirror of the
/// hand-rolled ProviderMessagesController logic, condensed to five methods.
/// </summary>
public sealed class EjarProviderChatStore : IChatStore
{
    public Task<bool> CanParticipateAsync(string conversationId, string userId, CancellationToken ct)
    {
        var c = EjarSeed.Conversations.FirstOrDefault(x => x.Id == conversationId);
        return Task.FromResult(c is not null && c.PartnerId == userId);
    }

    public Task<IChatMessage> AppendMessageAsync(string conversationId, string senderId, string body, CancellationToken ct)
    {
        var ix = EjarSeed.Conversations.FindIndex(c => c.Id == conversationId);
        if (ix < 0) throw new InvalidOperationException("conversation_not_found");
        var conv = EjarSeed.Conversations[ix];
        var msg  = new EjarSeed.MessageSeed(
            $"M-{conv.Messages.Count + 1}", conversationId, senderId, body, DateTime.UtcNow);
        conv.Messages.Add(msg);
        EjarSeed.Conversations[ix] = conv with { LastAt = msg.SentAt, UnreadCount = 0 };
        return Task.FromResult<IChatMessage>(msg);
    }

    public Task<IReadOnlyList<IChatMessage>> GetMessagesAsync(string conversationId, CancellationToken ct)
    {
        var c = EjarSeed.Conversations.FirstOrDefault(x => x.Id == conversationId);
        IReadOnlyList<IChatMessage> rows = c is null
            ? Array.Empty<IChatMessage>()
            : c.Messages.Cast<IChatMessage>().ToList();
        return Task.FromResult(rows);
    }

    public Task<IChatConversation?> GetConversationAsync(string conversationId, CancellationToken ct)
    {
        var c = EjarSeed.Conversations.FirstOrDefault(x => x.Id == conversationId);
        return Task.FromResult<IChatConversation?>(c);
    }

    public Task<IReadOnlyList<IChatConversation>> ListForUserAsync(string userId, CancellationToken ct)
    {
        IReadOnlyList<IChatConversation> rows = EjarSeed.Conversations
            .Where(c => c.PartnerId == userId)
            .Cast<IChatConversation>()
            .ToList();
        return Task.FromResult(rows);
    }
}
