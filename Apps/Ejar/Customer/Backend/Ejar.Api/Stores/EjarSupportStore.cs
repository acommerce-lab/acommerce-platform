using ACommerce.Kits.Chat.Backend;
using ACommerce.Kits.Support.Backend;
using ACommerce.Kits.Support.Domain;
using ACommerce.Kits.Support.Operations;
using Ejar.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Stores;

/// <summary>
/// مخزن تذاكر الدعم — يربط Support kit بـ <see cref="EjarDbContext"/>.
/// كلّ تذكرة = صفّ في <c>SupportTickets</c> + Conversation في Chat kit.
/// كلّ رسالة (الجسم الأوّل + الردود + رسائل النظام عن تغيير الحالة) تمرّ
/// على <see cref="IChatStore.AppendMessageAsync"/> فترث realtime broadcast
/// + DB notification + FCM push بلا كود إضافيّ.
///
/// <para>التذرّيّة: فتح التذكرة يُنشئ Conversation + SupportTicket في
/// نفس <c>SaveChangesAsync</c>. لو فشل أيّ منهما لا يتسرّب نصف-حالة لـ DB.</para>
/// </summary>
public sealed class EjarSupportStore : ISupportStore
{
    private readonly EjarDbContext _db;
    private readonly IChatStore _chat;
    private readonly SupportKitOptions _options;

    public EjarSupportStore(EjarDbContext db, IChatStore chat, SupportKitOptions options)
    {
        _db = db; _chat = chat; _options = options;
    }

    public async Task<bool> CanAccessAsync(string ticketId, string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(ticketId, out var tid)) return false;
        if (!Guid.TryParse(userId, out var uid))   return false;
        return await _db.SupportTickets.AsNoTracking()
            .AnyAsync(t => t.Id == tid && t.UserId == uid, ct);
    }

    public async Task<IReadOnlyList<ISupportTicket>> ListForUserAsync(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var uid)) return Array.Empty<ISupportTicket>();
        var rows = await _db.SupportTickets.AsNoTracking()
            .Where(t => t.UserId == uid)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ToListAsync(ct);
        return rows.Cast<ISupportTicket>().ToList();
    }

    public async Task<ISupportTicket?> GetAsync(string ticketId, CancellationToken ct)
    {
        if (!Guid.TryParse(ticketId, out var tid)) return null;
        var t = await _db.SupportTickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tid, ct);
        return t;
    }

    public async Task<ISupportTicket> OpenAsync(
        string userId, string subject, string initialMessage, string priority,
        string? relatedEntityId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var uid))
            throw new InvalidOperationException("invalid_user_id");

        var convId    = Guid.NewGuid();
        var ticketId  = Guid.NewGuid();
        var agentPool = ResolveAgentPoolId();

        // ① Conversation: مالكها = الشاكي، شريكها = pool الدعم.
        var conv = new ConversationEntity
        {
            Id          = convId,
            CreatedAt   = DateTime.UtcNow,
            OwnerId     = uid,
            PartnerId   = agentPool,
            ListingId   = Guid.Empty,             // لا تُربط بإعلان
            PartnerName = _options.AgentPoolDisplayName,
            Subject     = subject.Length > 200 ? subject[..200] : subject,
            LastAt      = DateTime.UtcNow,
            UnreadCount = 0,
        };
        _db.Conversations.Add(conv);

        // ② SupportTicket — يربط نفسه بالـ Conversation.
        var ticket = new SupportTicket
        {
            Id              = ticketId,
            CreatedAt       = DateTime.UtcNow,
            UserId          = uid,
            ConversationId  = convId,
            Subject         = subject.Length > 200 ? subject[..200] : subject,
            Status          = "open",
            Priority        = priority,
            RelatedEntityId = relatedEntityId,
        };
        _db.SupportTickets.Add(ticket);

        await _db.SaveChangesAsync(ct);

        // ③ الرسالة الأولى — تمرّ على Chat kit فيرث البثّ + الإشعارات.
        // ملاحظة: AppendMessageAsync داخلياً يحفظ + يبثّ لـ realtime + يُنشئ
        // notification entity للمستلم + FCM push. إذا فشلت لاحقاً (مثلاً
        // realtime down)، التذكرة + المحادثة محفوظتان فعلاً، فيظهر للمستخدم
        // أنّه أنشأ تذكرة بدون رسالة أولى — حالة شاذّة لكن غير مدمّرة.
        await _chat.AppendMessageAsync(convId.ToString(), userId, initialMessage, ct);

        return ticket;
    }

    public async Task<bool> SetStatusAsync(string ticketId, string newStatus, CancellationToken ct)
    {
        if (!Guid.TryParse(ticketId, out var tid)) return false;
        var t = await _db.SupportTickets.FirstOrDefaultAsync(x => x.Id == tid, ct);
        if (t is null) return false;

        var oldStatus = t.Status;
        t.Status    = newStatus;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // رسالة "نظام" داخل المحادثة — تُسلَّم للطرفَين عبر مسار chat.message
        // المعتاد فيظهر لها toast + notification في DB + FCM. <System> هو
        // userId ليتمكّن الواجهة من تمييز رسائل النظام بصرياً.
        var systemBody = $"[تغيّرت الحالة: {LabelStatus(oldStatus)} → {LabelStatus(newStatus)}]";
        try
        {
            await _chat.AppendMessageAsync(t.ConversationId.ToString(), "00000000-0000-0000-0000-000000000000",
                systemBody, ct);
        }
        catch { /* فشل بثّ الرسالة النظاميّة غير قاتل — التذكرة محفوظة. */ }

        return true;
    }

    public async Task<bool> AssignAgentAsync(string ticketId, string agentId, string agentDisplayName, CancellationToken ct)
    {
        if (!Guid.TryParse(ticketId, out var tid))     return false;
        if (!Guid.TryParse(agentId, out var aid))      return false;
        var t = await _db.SupportTickets.FirstOrDefaultAsync(x => x.Id == tid, ct);
        if (t is null) return false;

        t.AssignedAgentId = aid;
        t.UpdatedAt       = DateTime.UtcNow;

        // حدِّث المحادثة المرتبطة لتنعكس على inbox المستخدم بشكل سليم.
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == t.ConversationId, ct);
        if (conv is not null)
        {
            conv.PartnerId   = aid;
            conv.PartnerName = agentDisplayName;
            conv.UpdatedAt   = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private Guid ResolveAgentPoolId()
    {
        if (_options.AgentPoolPartyId.HasValue) return _options.AgentPoolPartyId.Value;
        // أرجع GUID ثابت يمثّل "pool الدعم" حتى يُكوَّن صراحةً في appsettings.
        // قيمة محسوبة بدون state — كلّ التذاكر تُربط بنفس "المستلم" حتى يُخصَّص.
        return new Guid("00000000-0000-0000-0000-00000000d000");
    }

    private static string LabelStatus(string s) => s switch
    {
        "open"        => "مفتوحة",
        "in_progress" => "قيد المعالجة",
        "resolved"    => "محلولة",
        "closed"      => "مغلقة",
        _             => s,
    };
}
