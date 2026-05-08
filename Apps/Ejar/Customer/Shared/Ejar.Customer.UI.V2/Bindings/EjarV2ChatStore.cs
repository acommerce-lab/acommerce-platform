using ACommerce.Chat.Operations;
using ACommerce.Kits.Chat.Frontend.Customer.Stores;

namespace Ejar.Customer.UI.V2.Bindings;

/// <summary>
/// تَنفيذ V2 لـ <see cref="IChatStore"/>. لا realtime — V2 يَستهلك
/// <see cref="IChatApiClient"/> فقط (REST). إضافة realtime مُستقبلاً
/// تَتمّ على مُستوى Chat kit نَفسه (broadcast hub) لا في الـ app.
/// </summary>
public sealed class EjarV2ChatStore : IChatStore
{
    private readonly IChatApiClient _api;
    private readonly List<IChatMessage> _msgs = new();
    private List<ConversationSummary> _conversations = new();
    private string? _currentConvId;

    public EjarV2ChatStore(IChatApiClient api) => _api = api;

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
        // POST عبر kit api client سيَأتي مُستقبلاً — حالياً V2 يَكتب من
        // خلال مَسار kit api client مُباشرة (TODO: إضافة SendAsync لـ
        // IChatApiClient).
        Task.CompletedTask;

    public async Task MarkReadAsync(string conversationId, CancellationToken ct = default)
    {
        await _api.EnterAsync(conversationId, ct);
        Changed?.Invoke();
    }
}
