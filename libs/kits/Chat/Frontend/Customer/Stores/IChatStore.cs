using ACommerce.Chat.Operations;

namespace ACommerce.Kits.Chat.Frontend.Customer.Stores;

/// <summary>
/// store reactive لـ Chat على العميل. يَستهلك <see cref="IChatMessage"/>
/// (interface من Chat.Operations) — لا يَعرف عن app entities. تطبيق إيجار
/// يَربطه بـ <c>EjarChatStore</c> الذي يَستعمل realtime + REST لتحميل
/// الرسائل وبثّ الإرسال.
/// </summary>
public interface IChatStore
{
    /// <summary>قائمة المحادثات الحاليّة (آخِر رسالة، عدّاد غير مقروء).</summary>
    IReadOnlyList<ConversationSummary> Conversations { get; }

    /// <summary>رسائل محادثة محدَّدة بعد <see cref="OpenConversationAsync"/>.</summary>
    IReadOnlyList<IChatMessage> CurrentMessages { get; }

    /// <summary>id المحادثة المفتوحة حاليّاً (أو null).</summary>
    string? CurrentConversationId { get; }

    /// <summary>عدّاد الرسائل غير المقروءة عبر كلّ المحادثات (للبادج).</summary>
    int UnreadTotal { get; }

    bool IsLoading { get; }
    event Action? Changed;

    Task LoadConversationsAsync(CancellationToken ct = default);
    Task OpenConversationAsync(string conversationId, CancellationToken ct = default);
    Task SendAsync(string body, CancellationToken ct = default);
    Task MarkReadAsync(string conversationId, CancellationToken ct = default);
}

/// <summary>ملخّص محادثة في قائمة الـ inbox.</summary>
public sealed record ConversationSummary(
    string Id,
    string PartnerName,
    string? Subject,
    string? LastMessagePreview,
    DateTime LastAt,
    int UnreadCount);
