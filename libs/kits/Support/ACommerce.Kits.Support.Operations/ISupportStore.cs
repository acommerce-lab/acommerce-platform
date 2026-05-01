using ACommerce.Kits.Support.Domain;

namespace ACommerce.Kits.Support.Operations;

/// <summary>
/// أنواع عمليّات Support kit (OAM Type strings). كلّ مكان يبني <c>Entry.Create(...)</c>
/// يجب أن يستخدم هذه الثوابت بدل سلاسل سحريّة، ليتمكّن أيّ interceptor من
/// المطابقة مركزياً عبر <c>op.Type</c>.
///
/// <para>ملاحظة OAM: عند الردّ على تذكرة، Type = <c>message.send</c> (نفس
/// Chat kit) لا <c>ticket.reply</c> — ذلك ليُورِّث الردّ كلّ المعترضات
/// المسجَّلة على رسائل الدردشة (realtime، DB notification، FCM push) بلا
/// تكرار كود. للتمييز يضاف tag <c>kind=support</c> + <c>ticket_id</c>.</para>
/// </summary>
public static class SupportOperationTypes
{
    /// <summary>إنشاء تذكرة جديدة + محادثة + رسالة أولى.</summary>
    public const string TicketOpen        = "ticket.open";
    /// <summary>تغيير حالة (open → in_progress → resolved → closed).</summary>
    public const string TicketStatusChange = "ticket.status_change";
    /// <summary>تخصيص وكيل (يُحدِّث PartnerId على المحادثة المرتبطة).</summary>
    public const string TicketAssignAgent  = "ticket.assign_agent";

    /// <summary>الردّ على تذكرة = نفس النوع المُستخدم في Chat kit.</summary>
    public const string TicketReply = "message.send";
}

/// <summary>
/// أوسمة OAM المعياريّة لعمليّات Support kit. مفتاحيّة للـ interceptors:
/// المعترض يطابق على <c>tag(kind, support)</c> ليفصل تذاكر الدعم عن
/// رسائل الدردشة العاديّة.
/// </summary>
public static class SupportTags
{
    public const string Kind     = "kind";
    public const string KindSupport = "support";
    public const string TicketId = "ticket_id";
    public const string FromStatus = "from_status";
    public const string ToStatus   = "to_status";
}

/// <summary>
/// عقد التخزين — الـ kit لا يفترض أيّ شكل DB. التطبيق يُنفِّذه في
/// <c>EjarSupportStore</c> (أو ما يماثله). الـ store يدير:
/// <list type="bullet">
///   <item>إنشاء التذكرة + المحادثة المرتبطة + الرسالة الأولى ذرّياً.</item>
///   <item>قراءة قائمة التذاكر (مع unread/last preview من Conversation).</item>
///   <item>تغيير الحالة، تخصيص الوكيل.</item>
/// </list>
///
/// <para>الردّ نفسه (نصّ من المستخدم/الوكيل) لا يمرّ هنا — يمرّ على
/// <c>IChatStore.AppendMessageAsync</c> مع <c>ConversationId</c> الخاصّ
/// بالتذكرة (الذي يحصل عليه الـ controller من <see cref="GetAsync"/>).</para>
/// </summary>
public interface ISupportStore
{
    /// <summary>هل هذه التذكرة تخصّ هذا المستخدم (للـ scoping)؟</summary>
    Task<bool> CanAccessAsync(string ticketId, string userId, CancellationToken ct);

    /// <summary>قائمة تذاكر المستخدم — مرتّبة من الأحدث، مع آخر رسالة + unread إن أمكن.</summary>
    Task<IReadOnlyList<ISupportTicket>> ListForUserAsync(string userId, CancellationToken ct);

    /// <summary>تذكرة واحدة (مع التحقّق من ملكيّة المستخدم في الـ controller).</summary>
    Task<ISupportTicket?> GetAsync(string ticketId, CancellationToken ct);

    /// <summary>فتح تذكرة: ينشئ Conversation عبر مزوّد الدردشة، ثمّ Ticket
    /// يُلصِقه عليها، ثمّ يضع <paramref name="initialMessage"/> رسالةً أولى.
    /// كلّ ذلك ذرّياً (نفس SaveChanges).</summary>
    Task<ISupportTicket> OpenAsync(
        string userId,
        string subject,
        string initialMessage,
        string priority,
        string? relatedEntityId,
        CancellationToken ct);

    /// <summary>تغيير الحالة. يستدعي appendMessageAsync داخلياً لإنشاء رسالة
    /// نظام في المحادثة (FromRole=system) ليصل الإشعار للمستخدم عبر مسار
    /// chat.message دون كود إضافيّ.</summary>
    Task<bool> SetStatusAsync(string ticketId, string newStatus, CancellationToken ct);

    /// <summary>تخصيص وكيل. يُحدِّث AssignedAgentId + PartnerId على المحادثة.</summary>
    Task<bool> AssignAgentAsync(string ticketId, string agentId, string agentDisplayName, CancellationToken ct);
}
