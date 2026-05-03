using ACommerce.Chat.Client.Blazor;
using ACommerce.Chat.Operations;
using ACommerce.Kits.Chat.Frontend.Customer.Stores;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="IChatStore"/> لإيجار. يَلفّ <see cref="IChatClient"/>
/// (للإرسال + استقبال realtime — التنفيذ الفعليّ <c>EjarChatClient</c>) +
/// <see cref="ApiReader"/> (لجلب القائمة).
/// </summary>
public sealed class EjarChatStore : IChatStore, IDisposable
{
    private readonly IChatClient _chat;
    private readonly ApiReader _api;
    private readonly List<IChatMessage> _msgs = new();
    private List<ConversationSummary> _conversations = new();

    public EjarChatStore(IChatClient chat, ApiReader api)
    {
        _chat = chat;
        _api  = api;
        _chat.MessageReceived += OnMessageReceived;
    }

    public IReadOnlyList<ConversationSummary> Conversations => _conversations;
    public IReadOnlyList<IChatMessage> CurrentMessages => _msgs;
    public string? CurrentConversationId => _chat.ActiveConversationId;
    public int UnreadTotal => _conversations.Sum(c => c.UnreadCount);
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadConversationsAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _api.GetAsync<List<ConversationSummary>>("/conversations", ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                _conversations = env.Data;
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task OpenConversationAsync(string conversationId, CancellationToken ct = default)
    {
        await _chat.EnterAsync(conversationId);
        var env = await _api.GetAsync<List<ChatMessageDto>>(
            $"/conversations/{Uri.EscapeDataString(conversationId)}/messages", ct: ct);
        _msgs.Clear();
        if (env.Operation.Status == "Success" && env.Data is not null)
            _msgs.AddRange(env.Data);
        Changed?.Invoke();
    }

    public Task SendAsync(string body, CancellationToken ct = default) =>
        _chat.SendAsync(body);

    public async Task MarkReadAsync(string conversationId, CancellationToken ct = default)
    {
        await _api.PostAsync<object>(
            $"/conversations/{Uri.EscapeDataString(conversationId)}/read", null, ct);
        Changed?.Invoke();
    }

    private void OnMessageReceived(IChatMessage msg)
    {
        _msgs.Add(msg);
        Changed?.Invoke();
    }

    public void Dispose() => _chat.MessageReceived -= OnMessageReceived;

    /// <summary>DTO يُحقّق <see cref="IChatMessage"/> مباشرةً (Law 6).</summary>
    private sealed class ChatMessageDto : IChatMessage
    {
        public string Id { get; set; } = "";
        public string ConversationId { get; set; } = "";
        public string SenderPartyId { get; set; } = "";
        public string Body { get; set; } = "";
        public DateTime SentAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
