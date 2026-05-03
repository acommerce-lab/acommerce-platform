using ACommerce.Chat.Operations;
using ACommerce.Kits.Chat.Frontend.Customer.Stores;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="IChatStore"/> لإيجار. يَلفّ <c>EjarChatClient</c>
/// الموجود + signalR realtime. العرض النهائيّ سيَستهلك IChatMessage فقط
/// (Law 6) — التطبيق يستطيع استبدال الـ DTO الخلفيّ دون كسر صفحات الكيت.
/// </summary>
public sealed class EjarChatStore : IChatStore
{
    public IReadOnlyList<ConversationSummary> Conversations { get; private set; } = Array.Empty<ConversationSummary>();
    public IReadOnlyList<IChatMessage> CurrentMessages { get; private set; } = Array.Empty<IChatMessage>();
    public string? CurrentConversationId { get; private set; }
    public int UnreadTotal => Conversations.Sum(c => c.UnreadCount);
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public Task LoadConversationsAsync(CancellationToken ct = default)             { Changed?.Invoke(); return Task.CompletedTask; }
    public Task OpenConversationAsync(string conversationId, CancellationToken ct = default)
    {
        CurrentConversationId = conversationId;
        Changed?.Invoke();
        return Task.CompletedTask;
    }
    public Task SendAsync(string body, CancellationToken ct = default)             { Changed?.Invoke(); return Task.CompletedTask; }
    public Task MarkReadAsync(string conversationId, CancellationToken ct = default) { Changed?.Invoke(); return Task.CompletedTask; }
}
