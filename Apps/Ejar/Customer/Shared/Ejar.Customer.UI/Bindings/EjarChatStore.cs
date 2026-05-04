using ACommerce.Chat.Client.Blazor;
using ACommerce.Chat.Operations;
using ACommerce.Kits.Chat.Frontend.Customer.Stores;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="IChatStore"/> لإيجار. يَلفّ <see cref="IChatClient"/>
/// (للإرسال + استقبال realtime) + <see cref="IChatApiClient"/> (لجلب
/// القائمة والتاريخ). لا shape/JSON هنا.
/// </summary>
public sealed class EjarChatStore : IChatStore, IDisposable
{
    private readonly IChatClient _chat;
    private readonly IChatApiClient _api;
    private readonly List<IChatMessage> _msgs = new();
    private List<ConversationSummary> _conversations = new();

    public EjarChatStore(IChatClient chat, IChatApiClient api)
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
        try   { _conversations = (await _api.ListConversationsAsync(ct)).ToList(); }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task OpenConversationAsync(string conversationId, CancellationToken ct = default)
    {
        await _chat.EnterAsync(conversationId);
        var msgs = await _api.ListMessagesAsync(conversationId, ct);
        _msgs.Clear();
        _msgs.AddRange(msgs);
        Changed?.Invoke();
    }

    public Task SendAsync(string body, CancellationToken ct = default) =>
        _chat.SendAsync(body);

    public async Task MarkReadAsync(string conversationId, CancellationToken ct = default)
    {
        await _api.EnterAsync(conversationId, ct);
        Changed?.Invoke();
    }

    private void OnMessageReceived(IChatMessage msg)
    {
        _msgs.Add(msg);
        Changed?.Invoke();
    }

    public void Dispose() => _chat.MessageReceived -= OnMessageReceived;
}
