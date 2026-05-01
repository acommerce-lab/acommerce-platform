using ACommerce.SharedKernel.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace ACommerce.Kits.Support.Domain;

/// <summary>
/// عقد التذكرة الأدنى الذي يستهلكه الـ kit. التطبيق يُلصِقه على entity
/// قاعدة البيانات الفعليّة (مثلاً <c>SupportTicket</c>) — راجع Law 6 في
/// <c>CLAUDE.md</c>: المكتبة تتعامل مع interface فقط، الـ App يحتفظ بشكل
/// التخزين الذي يناسبه.
///
/// <para>الفلسفة: تذكرة الدعم = محادثة (Chat kit) + metadata (status,
/// priority, …). كلّ المراسلات تعيش في <c>ConversationId</c> المرتبط، فلا
/// يحتاج Support kit جداول رسائل خاصّة به أو كود broadcast/notification —
/// كلّه مجّاناً عبر Chat kit + الـ EjarCustomerChatStore (realtime + DB
/// notification + FCM push).</para>
/// </summary>
public interface ISupportTicket
{
    string Id { get; }
    string UserId { get; }
    /// <summary>FK إلى <c>ConversationEntity</c> في Chat kit. كلّ ردود
    /// التذكرة (الجسم الأوّل + ردود الوكيل + ردود المستخدم) رسائل في هذه المحادثة.</summary>
    string ConversationId { get; }
    string Subject { get; }
    /// <summary>open | in_progress | resolved | closed</summary>
    string Status { get; }
    /// <summary>normal | high | urgent</summary>
    string Priority { get; }
    /// <summary>اختياريّ: معرّف كيان مرتبط (إعلان، حجز، …) — لتوجيه السياق.</summary>
    string? RelatedEntityId { get; }
    /// <summary>اختياريّ: الوكيل المخصَّص. إن كان null، التذكرة في pool الدعم.</summary>
    string? AssignedAgentId { get; }
    DateTime CreatedAt { get; }
    DateTime? UpdatedAt { get; }
}

/// <summary>
/// كيان قاعدة بيانات التذكرة. <see cref="ISupportTicket"/> يطابقه مباشرةً.
///
/// <para>ملاحظة: لا يحوي <c>Body</c> ولا <c>Replies</c> — هذان كانا في النسخة
/// السابقة قبل ربط Chat kit. الآن أوّل رسالة في <c>ConversationId</c> هي
/// "الجسم"، وكلّ ردّ لاحق رسالة في نفس المحادثة. إعادة الاستخدام كاملة:
/// realtime broadcast + toast في الواجهة + FCM push + persistence — كلّها
/// تعمل تلقائياً عبر مسار chat.message.</para>
/// </summary>
public class SupportTicket : IBaseEntity, ISupportTicket
{
    [Key] public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }
    public Guid ConversationId { get; set; }

    [MaxLength(200)] public string Subject { get; set; } = "";
    [MaxLength(20)]  public string Status { get; set; } = "open";
    [MaxLength(20)]  public string Priority { get; set; } = "normal";
    [MaxLength(100)] public string? RelatedEntityId { get; set; }
    public Guid? AssignedAgentId { get; set; }

    // ── ISupportTicket (string projections لـ kit) ──────────────────────
    string ISupportTicket.Id => Id.ToString();
    string ISupportTicket.UserId => UserId.ToString();
    string ISupportTicket.ConversationId => ConversationId.ToString();
    string? ISupportTicket.AssignedAgentId => AssignedAgentId?.ToString();
}
