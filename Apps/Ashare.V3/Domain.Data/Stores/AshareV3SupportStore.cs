using ACommerce.Kits.Support.Backend;
using ACommerce.Kits.Support.Domain;
using ACommerce.Kits.Support.Operations;
using Ashare.V3.Data;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Api.Stores;

/// <summary>
/// مخزن تذاكر الدعم — مَعزول تماماً عن Chat kit. كلّ تذكرة + كلّ
/// رسائلها تَعيش في <c>SupportTickets</c> + <c>SupportMessages</c>،
/// فلا تَظهر في <c>/conversations</c> ولا تُحَفِّز chat interceptors
/// (FCM، broadcast، persistent-notif).
/// </summary>
public sealed class AshareV3SupportStore : ISupportStore
{
    private readonly AshareV3DbContext _db;
    private readonly SupportKitOptions _options;

    public AshareV3SupportStore(AshareV3DbContext db, SupportKitOptions options)
    {
        _db = db; _options = options;
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

    public async Task<IReadOnlyList<ISupportMessage>> GetMessagesAsync(string ticketId, CancellationToken ct)
    {
        if (!Guid.TryParse(ticketId, out var tid)) return Array.Empty<ISupportMessage>();
        var rows = await _db.SupportMessages.AsNoTracking()
            .Where(m => m.TicketId == tid)
            .OrderBy(m => m.SentAt)
            .ToListAsync(ct);
        return rows.Select(m => (ISupportMessage)new SupportMessageView(m)).ToList();
    }

    public async Task AddNoSaveAsync(ISupportTicket ticket, string initialMessageBody, CancellationToken ct)
    {
        if (!Guid.TryParse(ticket.UserId, out var uid))
            throw new InvalidOperationException("invalid_user_id");
        var ticketId = Guid.TryParse(ticket.Id, out var tid) ? tid : Guid.NewGuid();

        // ConversationId: عمود قديم لا يَستخدمه أحد بَعد. نَملؤه بـ
        // Guid.NewGuid() لإرضاء أيّ unique index قديم باقٍ في DB. لاحقاً
        // migration يُسقط العمود.
        var entity = new SupportTicket
        {
            Id              = ticketId,
            CreatedAt       = ticket.CreatedAt,
            UserId          = uid,
            ConversationId  = Guid.NewGuid(),
            Subject         = ticket.Subject.Length > 200 ? ticket.Subject[..200] : ticket.Subject,
            Status          = ticket.Status,
            Priority        = ticket.Priority,
            RelatedEntityId = ticket.RelatedEntityId,
        };
        _db.SupportTickets.Add(entity);

        var initialMsg = new SupportMessageEntity
        {
            Id            = Guid.NewGuid(),
            CreatedAt     = ticket.CreatedAt,
            TicketId      = ticketId,
            SenderPartyId = $"User:{uid}",
            Body          = initialMessageBody,
            SentAt        = ticket.CreatedAt,
        };
        _db.SupportMessages.Add(initialMsg);
        await Task.CompletedTask;
    }

    public async Task AppendMessageNoSaveAsync(string ticketId, string senderPartyId, string body, CancellationToken ct)
    {
        if (!Guid.TryParse(ticketId, out var tid)) return;

        var msg = new SupportMessageEntity
        {
            Id            = Guid.NewGuid(),
            CreatedAt     = DateTime.UtcNow,
            TicketId      = tid,
            SenderPartyId = senderPartyId,
            Body          = body,
            SentAt        = DateTime.UtcNow,
        };
        _db.SupportMessages.Add(msg);

        // bump ticket UpdatedAt — لِيُرَتَّب inbox الدعم من الأحدث.
        var t = _db.SupportTickets.Local.FirstOrDefault(x => x.Id == tid)
              ?? await _db.SupportTickets.FirstOrDefaultAsync(x => x.Id == tid, ct);
        if (t is not null) t.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<bool> SetStatusAsync(string ticketId, string newStatus, CancellationToken ct)
    {
        if (!Guid.TryParse(ticketId, out var tid)) return false;
        var t = await _db.SupportTickets.FirstOrDefaultAsync(x => x.Id == tid, ct);
        if (t is null) return false;

        var oldStatus = t.Status;
        t.Status    = newStatus;
        t.UpdatedAt = DateTime.UtcNow;

        // رسالة "نظام" داخل التذكرة فقط — لا broadcast ولا FCM (مَعزول).
        var systemBody = $"[تغيّرت الحالة: {LabelStatus(oldStatus)} → {LabelStatus(newStatus)}]";
        _db.SupportMessages.Add(new SupportMessageEntity
        {
            Id            = Guid.NewGuid(),
            CreatedAt     = DateTime.UtcNow,
            TicketId      = tid,
            SenderPartyId = "System",
            Body          = systemBody,
            SentAt        = DateTime.UtcNow,
        });
        return true;
    }

    public async Task<bool> AssignAgentAsync(string ticketId, string agentId, string agentDisplayName, CancellationToken ct)
    {
        if (!Guid.TryParse(ticketId, out var tid)) return false;
        if (!Guid.TryParse(agentId, out var aid))  return false;
        var t = await _db.SupportTickets.FirstOrDefaultAsync(x => x.Id == tid, ct);
        if (t is null) return false;

        t.AssignedAgentId = aid;
        t.UpdatedAt       = DateTime.UtcNow;
        return true;
    }

    private static string LabelStatus(string s) => s switch
    {
        "open"        => "مفتوحة",
        "in_progress" => "قيد المعالجة",
        "resolved"    => "محلولة",
        "closed"      => "مغلقة",
        _             => s,
    };

    private sealed class SupportMessageView : ISupportMessage
    {
        private readonly SupportMessageEntity _e;
        public SupportMessageView(SupportMessageEntity e) => _e = e;
        public string Id            => _e.Id.ToString();
        public string TicketId      => _e.TicketId.ToString();
        public string SenderPartyId => _e.SenderPartyId;
        public string Body          => _e.Body;
        public DateTime SentAt      => _e.SentAt;
    }
}
