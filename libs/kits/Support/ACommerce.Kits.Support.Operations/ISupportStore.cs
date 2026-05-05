using ACommerce.Kits.Support.Domain;
using ACommerce.OperationEngine.Core;

namespace ACommerce.Kits.Support.Operations;

/// <summary>
/// أنواع عمليّات Support kit (typed). كلّ <c>Entry.Create(...)</c> يجب أن
/// يستخدم هذه الأنواع بدل سلاسل، ليتمكّن أيّ interceptor من المطابقة
/// المتطابقة عبر <c>op.Type == SupportOps.X</c>.
///
/// <para>الردّ على تذكرة الآن نوع OAM مُستقلّ <c>"ticket.reply"</c> — لا
/// يَرث chat interceptors (FCM, persistent-notif, broadcast). تذاكر الدعم
/// مَعزولة تماماً عن الدردشة.</para>
/// </summary>
public static class SupportOps
{
    /// <summary>إنشاء تذكرة جديدة + رسالة أولى.</summary>
    public static readonly OperationType TicketOpen         = new("ticket.open");
    /// <summary>تغيير حالة (open → in_progress → resolved → closed).</summary>
    public static readonly OperationType TicketStatusChange = new("ticket.status_change");
    /// <summary>تخصيص وكيل.</summary>
    public static readonly OperationType TicketAssignAgent  = new("ticket.assign_agent");
    /// <summary>الردّ على تذكرة — نوع مُستقلّ، مَعزول عن chat.message.send.</summary>
    public static readonly OperationType TicketReply        = new("ticket.reply");
}

/// <summary>
/// عقد رسالة دعم — مَعزول عن <c>IChatMessage</c>. كلّ رسالة في تذكرة
/// (الجسم الأوّل + ردود) تَعيش في جدول <c>SupportMessages</c> الخاصّ
/// بالدعم، لا في جدول الدردشة.
/// </summary>
public interface ISupportMessage
{
    string Id { get; }
    string TicketId { get; }
    /// <summary>"User:GUID" للمستخدِم/الوكيل، أو "System" لرسائل تَلقائيّة.</summary>
    string SenderPartyId { get; }
    string Body { get; }
    DateTime SentAt { get; }
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
/// <c>EjarSupportStore</c> (أو ما يماثله). الـ store يدير كامل دورة الحياة
/// للتذكرة + رسائلها — مَعزول تماماً عن <c>IChatStore</c>.
/// </summary>
public interface ISupportStore
{
    /// <summary>هل هذه التذكرة تخصّ هذا المستخدم (للـ scoping)؟</summary>
    Task<bool> CanAccessAsync(string ticketId, string userId, CancellationToken ct);

    /// <summary>قائمة تذاكر المستخدم — مرتّبة من الأحدث.</summary>
    Task<IReadOnlyList<ISupportTicket>> ListForUserAsync(string userId, CancellationToken ct);

    /// <summary>تذكرة واحدة (مع التحقّق من ملكيّة المستخدم في الـ controller).</summary>
    Task<ISupportTicket?> GetAsync(string ticketId, CancellationToken ct);

    /// <summary>رسائل تذكرة — مُرتَّبة من الأقدم.</summary>
    Task<IReadOnlyList<ISupportMessage>> GetMessagesAsync(string ticketId, CancellationToken ct);

    /// <summary>
    /// يُسجِّل التذكرة + الرسالة الأولى على tracker (F6: لا
    /// <c>SaveChangesAsync</c>). default no-op يجعل <c>ticket.open</c> ينجح
    /// كحدث OAM حتّى دون جدول.
    /// </summary>
    Task AddNoSaveAsync(
        ISupportTicket ticket,
        string initialMessageBody,
        CancellationToken ct) => Task.CompletedTask;

    /// <summary>يُلحق رسالة بالتذكرة على tracker (F6: لا SaveChanges).</summary>
    Task AppendMessageNoSaveAsync(
        string ticketId,
        string senderPartyId,
        string body,
        CancellationToken ct) => Task.CompletedTask;

    /// <summary>تغيير الحالة + رسالة نظام. F6: tracker فقط.</summary>
    Task<bool> SetStatusAsync(string ticketId, string newStatus, CancellationToken ct);

    /// <summary>تخصيص وكيل.</summary>
    Task<bool> AssignAgentAsync(string ticketId, string agentId, string agentDisplayName, CancellationToken ct);
}
