using ACommerce.Chat.Operations;

namespace ACommerce.Kits.Chat.Backend;

/// <summary>
/// منفذ التخزين الذي يربط <see cref="ChatController"/> ببيانات التطبيق
/// (in-memory seed، EF، repository pattern…). التطبيق يكتب نفسه واحداً
/// يطابق طبقة بياناته، ويسجّله في <c>AddChatKit&lt;TStore&gt;</c>.
///
/// <para>
/// كلّ التوقيعات تستعمل <see cref="IChatMessage"/> و <see cref="IChatConversation"/>
/// كواجهات لا DTOs — كيان نطاق التطبيق ينفّذها مباشرة (Law 6 المعدَّل).
/// </para>
/// </summary>
public interface IChatStore
{
    /// <summary>هل <paramref name="userId"/> مشارك في المحادثة؟ يحدّد التفويض.</summary>
    Task<bool> CanParticipateAsync(string conversationId, string userId, CancellationToken ct);

    /// <summary>يضيف رسالة جديدة. ينشئ ID + SentAt. يرجع الرسالة المنشأة.</summary>
    Task<IChatMessage> AppendMessageAsync(string conversationId, string senderId, string body, CancellationToken ct);

    /// <summary>يجلب رسائل المحادثة مرتّبة بالأقدم أوّلاً.</summary>
    Task<IReadOnlyList<IChatMessage>> GetMessagesAsync(string conversationId, CancellationToken ct);

    /// <summary>تفاصيل المحادثة (المشاركون، الموضوع، …) أو <c>null</c> لو غير موجودة.</summary>
    Task<IChatConversation?> GetConversationAsync(string conversationId, CancellationToken ct);

    /// <summary>كلّ محادثات المستخدم — للـ inbox.</summary>
    Task<IReadOnlyList<IChatConversation>> ListForUserAsync(string userId, CancellationToken ct);
}
