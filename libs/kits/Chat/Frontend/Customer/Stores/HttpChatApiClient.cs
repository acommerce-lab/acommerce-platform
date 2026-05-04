using ACommerce.Chat.Operations;
using ACommerce.ClientHost.KitApi;

namespace ACommerce.Kits.Chat.Frontend.Customer.Stores;

public sealed class HttpChatApiClient : IChatApiClient
{
    private const string Kit = "chat";
    private readonly KitHttpClient _http;

    public HttpChatApiClient(KitHttpClient http) => _http = http;

    public async Task<IReadOnlyList<ConversationSummary>> ListConversationsAsync(CancellationToken ct = default)
    {
        var res = await _http.GetAsync<List<ConversationSummary>>(Kit, "/conversations", ct);
        return res.Success && res.Data is not null ? res.Data : Array.Empty<ConversationSummary>();
    }

    public async Task<IReadOnlyList<IChatMessage>> ListMessagesAsync(string conversationId, CancellationToken ct = default)
    {
        var res = await _http.GetAsync<List<ChatMessageDto>>(Kit,
            $"/conversations/{Uri.EscapeDataString(conversationId)}/messages", ct);
        return res.Success && res.Data is not null
            ? res.Data.Cast<IChatMessage>().ToList()
            : Array.Empty<IChatMessage>();
    }

    public async Task<bool> EnterAsync(string conversationId, CancellationToken ct = default)
    {
        var res = await _http.PostAsync<object>(Kit, $"/chat/{Uri.EscapeDataString(conversationId)}/enter", null, ct);
        return res.Success;
    }

    /// <summary>DTO يُحقّق <see cref="IChatMessage"/> مباشرة (Law 6).</summary>
    private sealed class ChatMessageDto : IChatMessage
    {
        public string Id { get; set; } = "";
        public string ConversationId { get; set; } = "";
        public string SenderPartyId  { get; set; } = "";
        public string Body { get; set; } = "";
        public DateTime SentAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
