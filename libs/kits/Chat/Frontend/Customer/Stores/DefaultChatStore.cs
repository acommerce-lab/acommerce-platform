using ACommerce.Chat.Operations;
using ACommerce.Client.Operations;

namespace ACommerce.Kits.Chat.Frontend.Customer.Stores;

/// <summary>
/// تَنفيذ افتراضيّ لـ <see cref="IChatStore"/> — OAM-shaped (F61).
/// كلّ سُلوك يُمَثَّل بِقَيد محاسبيّ يُرسَل عَبر <see cref="ITemplateEngine"/>.
/// compositions (Realtime، Optimistic، Telemetry) تَحقن مُعتَرضات عَلى
/// op type بدون لَمس هذا الـ store. compositions تَستَطيع أَيضاً
/// dispatch قُيود اصطِناعيّة (chat.message.received) لِتَحديث الحالة
/// مِن مَصادِر خارِجيّة (SignalR، WebSocket).
/// </summary>
public sealed class DefaultChatStore : IChatStore
{
    private readonly ITemplateEngine _engine;
    private readonly List<IChatMessage> _msgs = new();
    private List<ConversationSummary> _conversations = new();
    private string? _currentConvId;

    public DefaultChatStore(ITemplateEngine engine) => _engine = engine;

    public IReadOnlyList<ConversationSummary> Conversations => _conversations;
    public IReadOnlyList<IChatMessage> CurrentMessages => _msgs;
    public string? CurrentConversationId => _currentConvId;
    public int UnreadTotal => _conversations.Sum(c => c.UnreadCount);
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadConversationsAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _engine.ExecuteAsync<List<ConversationSummary>>(
                ChatOps.ListConversations(), ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                _conversations = env.Data;
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task OpenConversationAsync(string conversationId, CancellationToken ct = default)
    {
        _currentConvId = conversationId;
        await _engine.ExecuteAsync<object>(ChatOps.EnterConversation(conversationId), ct: ct);
        var env = await _engine.ExecuteAsync<ConversationDetailsDto>(
            ChatOps.OpenConversation(conversationId), ct: ct);
        _msgs.Clear();
        if (env.Operation.Status == "Success" && env.Data?.Messages is { } incoming)
            _msgs.AddRange(incoming.Cast<IChatMessage>());
        Changed?.Invoke();
    }

    public async Task SendAsync(string body, CancellationToken ct = default)
    {
        if (_currentConvId is null || string.IsNullOrWhiteSpace(body)) return;
        var env = await _engine.ExecuteAsync<object>(
            ChatOps.SendMessage(_currentConvId, body),
            payload: new { text = body },
            ct: ct);
        if (env.Operation.Status == "Success") Changed?.Invoke();
    }

    public async Task MarkReadAsync(string conversationId, CancellationToken ct = default)
    {
        await _engine.ExecuteAsync<object>(ChatOps.EnterConversation(conversationId), ct: ct);
        Changed?.Invoke();
    }

    /// <summary>
    /// مَدخَل لِـ compositions (مَثلاً Realtime) لِتَدفَع رَسالة وارِدَة
    /// مِن SignalR إلى الحالة المَحَلّيّة بدون أن تَدور عَلى السيرفر.
    /// </summary>
    public void IngestRealtimeMessage(IChatMessage message)
    {
        if (message.ConversationId == _currentConvId)
        {
            _msgs.Add(message);
            Changed?.Invoke();
        }
        else
        {
            // زِيادَة العَدّاد فقط — السيرفر يُمَلّك عَدّاد المُحادَثة لاحِقاً.
            var idx = _conversations.FindIndex(c => c.Id == message.ConversationId);
            if (idx >= 0)
            {
                var c = _conversations[idx];
                _conversations[idx] = c with { UnreadCount = c.UnreadCount + 1 };
                Changed?.Invoke();
            }
        }
    }

    /// <summary>Wire shape لِـ <c>GET /conversations/{id}</c>.</summary>
    private sealed class ConversationDetailsDto
    {
        public List<ChatMessageDto>? Messages { get; set; }
    }

    /// <summary>POCO يُحَقِّق <see cref="IChatMessage"/> لِنَتائج HTTP.</summary>
    private sealed record ChatMessageDto(
        string Id, string ConversationId, string SenderPartyId,
        string Body, DateTime SentAt, DateTime? ReadAt) : IChatMessage;
}
