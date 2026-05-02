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
    [Obsolete("استعمل AppendNoSaveAsync بدلاً منها — يقبل IChatMessage مبنيّاً مسبقاً ولا يحفظ بنفسه (F6: SaveAtEnd على القيد). تبقى هنا للتوافق مع SupportController.Reply ومسارات أخرى لم تُرحَّل بعد.")]
    Task<IChatMessage> AppendMessageAsync(string conversationId, string senderId, string body, CancellationToken ct);

    /// <summary>
    /// يُسجِّل رسالة على الـ tracker (F6: لا <c>SaveChangesAsync</c>).
    /// المُتّصِل (<c>ChatController.Send</c>) يبني <see cref="IChatMessage"/>
    /// في الذاكرة أوّلاً (<see cref="InMemoryChatMessage"/>)، يضعه على
    /// <c>ctx.WithEntity&lt;IChatMessage&gt;()</c> ثمّ يستدعي هذه الدالّة لو
    /// أراد الـ persistence. لو الـ store غير مسجَّل أو هذه الدالّة لا تفعل
    /// شيئاً (no-op impl)، الرسالة لا تزال حدثاً OAM صالحاً يتدفّق لباقي
    /// المعترضات (broadcast، notification.create، FCM).
    ///
    /// <para>التطبيق الافتراضيّ هنا = no-op — التطبيقات التي تريد persistence
    /// تتجاوزه بحفظ tracked على DbContext.</para>
    /// </summary>
    Task AppendNoSaveAsync(IChatMessage message, CancellationToken ct) => Task.CompletedTask;

    /// <summary>يجلب رسائل المحادثة مرتّبة بالأقدم أوّلاً.</summary>
    Task<IReadOnlyList<IChatMessage>> GetMessagesAsync(string conversationId, CancellationToken ct);

    /// <summary>تفاصيل المحادثة (المشاركون، الموضوع، …) أو <c>null</c> لو غير موجودة.</summary>
    Task<IChatConversation?> GetConversationAsync(string conversationId, CancellationToken ct);

    /// <summary>كلّ محادثات المستخدم — للـ inbox.</summary>
    Task<IReadOnlyList<IChatConversation>> ListForUserAsync(string userId, CancellationToken ct);
}
