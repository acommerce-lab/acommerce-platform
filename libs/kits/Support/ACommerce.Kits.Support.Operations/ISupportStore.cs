using ACommerce.Kits.Support.Domain;
using ACommerce.OperationEngine.Core;

namespace ACommerce.Kits.Support.Operations;

/// <summary>
/// أنواع عمليّات Support kit (typed). كلّ <c>Entry.Create(...)</c> يجب أن
/// يستخدم هذه الأنواع بدل سلاسل، ليتمكّن أيّ interceptor من المطابقة
/// المتطابقة عبر <c>op.Type == SupportOps.X</c>.
///
/// <para>ملاحظة OAM: عند الردّ على تذكرة، Type = <c>MessageOps.Send</c>
/// (نفس Chat kit) لا op خاصّ — لتُورِّث المعترضات المسجَّلة على رسائل
/// الدردشة. للتمييز يضاف <see cref="SupportMarkers.IsTicketReply"/>.</para>
/// </summary>
public static class SupportOps
{
    /// <summary>إنشاء تذكرة جديدة + محادثة + رسالة أولى.</summary>
    public static readonly OperationType TicketOpen         = new("ticket.open");
    /// <summary>تغيير حالة (open → in_progress → resolved → closed).</summary>
    public static readonly OperationType TicketStatusChange = new("ticket.status_change");
    /// <summary>تخصيص وكيل (يُحدِّث PartnerId على المحادثة المرتبطة).</summary>
    public static readonly OperationType TicketAssignAgent  = new("ticket.assign_agent");
    /// <summary>الردّ على تذكرة = نفس النوع المُستخدم في Chat kit.</summary>
    public static readonly OperationType TicketReply        = new("message.send");
}

/// <summary>
/// مفاتيح أوسمة Support kit المُكتَّبة. التركيب الخارجيّ (composition) يطابق
/// عبرها بدل سلاسل، فيُكشَف أيّ خطأ كتابيّ وقت compile-time.
/// </summary>
public static class SupportTagKeys
{
    public static readonly TagKey Kind       = new("kind");
    public static readonly TagKey TicketId   = new("ticket_id");
    public static readonly TagKey FromStatus = new("from_status");
    public static readonly TagKey ToStatus   = new("to_status");
}

/// <summary>قيم الأوسمة الثابتة المعتبَرة.</summary>
public static class SupportTagValues
{
    public static readonly TagValue Support = new("support");
}

/// <summary>
/// Markers مُعلَّبة (key+value) — لا فرصة لتفكيك خطأ، يُكتب marker واحد
/// أينما لزم.
/// </summary>
public static class SupportMarkers
{
    /// <summary>"هذه العمليّة جزء من تذكرة دعم" — يضعها السطر الواحد على
    /// أيّ message.send يخصّ تذكرة، فيلتقطه SupportTicketBumpBundle.</summary>
    public static readonly Marker IsTicketReply = new(SupportTagKeys.Kind, SupportTagValues.Support);
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
