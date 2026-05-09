namespace ACommerce.Kits.Support.Domain;

/// <summary>
/// تطبيق POCO نقيّ لـ <see cref="ISupportTicket"/> — لا EF، لا DB.
/// يُستخدم في <c>SupportController.Open</c> كحدث OAM أصيل: الـ Execute
/// body يبنيه ويضعه على <c>ctx.WithEntity&lt;ISupportTicket&gt;()</c>.
///
/// <para>ملاحظة: على عكس <c>InMemoryReport</c> أو <c>InMemoryChatMessage</c>،
/// التذكرة <i>يصاحبها</i> Conversation وأوّل رسالة. الـ store قد يحفظ
/// الثلاثة ذرّيّاً (في تطبيق Ejar)، أو يحفظ الـ ticket فقط ويترك الرسائل
/// لـ Chat kit، أو لا يحفظ شيئاً (in-memory mode). الكلّ مقبول — العمليّة
/// نفسها OAM-pure.</para>
/// </summary>
public sealed record InMemorySupportTicket(
    string Id,
    string UserId,
    string ConversationId,
    string Subject,
    string Status,
    string Priority,
    string? RelatedEntityId,
    string? AssignedAgentId,
    DateTime CreatedAt,
    DateTime? UpdatedAt = null
) : ISupportTicket;
