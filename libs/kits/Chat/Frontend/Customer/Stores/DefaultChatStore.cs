using ACommerce.Chat.Operations;

namespace ACommerce.Kits.Chat.Frontend.Customer.Stores;

/// <summary>
/// تَنفيذ افتراضيّ لـ <see cref="IChatStore"/> يَدلّع لـ
/// <see cref="IChatApiClient"/> فقط — REST بدون realtime. التَطبيقات
/// التي تَحتاج SignalR/hub تَكتب Binding خاصّ يَلفّ هذا أو يَستبدله.
/// </summary>
public sealed class DefaultChatStore : IChatStore
{
    private readonly IChatApiClient _api;
    private readonly List<IChatMessage> _msgs = new();
    private List<ConversationSummary> _conversations = new();
    private string? _currentConvId;

    public DefaultChatStore(IChatApiClient api) => _api = api;

    public IReadOnlyList<ConversationSummary> Conversations => _conversations;
    public IReadOnlyList<IChatMessage> CurrentMessages => _msgs;
    public string? CurrentConversationId => _currentConvId;
    public int UnreadTotal => _conversations.Sum(c => c.UnreadCount);
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadConversationsAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try   { _conversations = (await _api.ListConversationsAsync(ct)).ToList(); }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task OpenConversationAsync(string conversationId, CancellationToken ct = default)
    {
        _currentConvId = conversationId;
        await _api.EnterAsync(conversationId, ct);
        var msgs = await _api.ListMessagesAsync(conversationId, ct);
        _msgs.Clear();
        _msgs.AddRange(msgs);
        Changed?.Invoke();
    }

    public Task SendAsync(string body, CancellationToken ct = default) =>
        // POST عبر kit api client سيَأتي مُستقبلاً — حالياً لا send REST.
        Task.CompletedTask;

    public async Task MarkReadAsync(string conversationId, CancellationToken ct = default)
    {
        await _api.EnterAsync(conversationId, ct);
        Changed?.Invoke();
    }
}
