using ACommerce.OperationEngine.Wire.Http;
using Ashare.V3.Data;
using Ashare.V3.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// نُقاط <c>/support/*</c> لِـ V3 — مُسطَّحَة فَوق
/// <see cref="ComplaintEntity"/> الإنتاجِي بَدَل ربط Chat الَّذي يَفرِضه
/// Support kit (V3.ComplaintEntity لا يَحوي <c>ConversationId</c>).
///
/// <para>الـ wire shape مُطابِق لِـ <c>HttpSupportApiClient</c>:</para>
/// <list type="bullet">
///   <item><c>GET  /support/tickets</c> → <c>List&lt;SupportTicketSummary&gt;</c></item>
///   <item><c>GET  /support/tickets/{id}</c> → كائِن تَفاصيل + رِسائِل</item>
///   <item><c>POST /support/tickets</c> body=<c>{subject, body}</c> → <c>{id}</c></item>
///   <item><c>POST /support/tickets/{id}/replies</c> body=<c>{body}</c> → ok</item>
/// </list>
///
/// <para>قَبل هذا الـ controller كان POST يَفشَل بِـ 500 (لا endpoint
/// مُسَجَّل) ⇒ frontend يَفُكّ الجِسم "S..." كَ JSON ⇒ <c>'S' is invalid</c>.</para>
/// </summary>
[ApiController]
[Authorize]
public sealed class SupportController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    public SupportController(AshareV3DbContext db) => _db = db;

    private string? CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("/support/tickets")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        var tickets = await _db.Complaints.AsNoTracking()
            .Where(c => c.UserId == CallerId)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Select(c => new
            {
                id           = c.Id.ToString(),
                subject      = c.Title,
                status       = c.Status,
                updatedAt    = c.UpdatedAt ?? c.CreatedAt,
                unreadReplies = 0, // V3 لا يَتَتَبَّع unread per-user عَلى الرُدود حاليّاً
            })
            .ToListAsync(ct);
        return this.OkEnvelope("support.tickets.list", tickets);
    }

    [HttpGet("/support/tickets/{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        if (!Guid.TryParse(id, out var cid)) return this.BadRequestEnvelope("invalid_id");

        var ticket = await _db.Complaints.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cid && c.UserId == CallerId, ct);
        if (ticket is null) return this.NotFoundEnvelope("ticket_not_found");

        var replies = await _db.ComplaintReplies.AsNoTracking()
            .Where(r => r.ComplaintId == cid)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new
            {
                id = r.Id.ToString(),
                ticketId = r.ComplaintId.ToString(),
                senderPartyId = r.IsStaff ? "Staff:" + r.SenderId : "User:" + r.SenderId,
                body = r.Message,
                sentAt = r.CreatedAt,
            })
            .ToListAsync(ct);

        return this.OkEnvelope("support.ticket.get", new
        {
            id          = ticket.Id.ToString(),
            subject     = ticket.Title,
            description = ticket.Description,
            status      = ticket.Status,
            priority    = ticket.Priority,
            createdAt   = ticket.CreatedAt,
            updatedAt   = ticket.UpdatedAt,
            messages    = replies,
        });
    }

    public sealed record CreateBody(string? Subject, string? Body);

    [HttpPost("/support/tickets")]
    public async Task<IActionResult> Create([FromBody] CreateBody body, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        if (string.IsNullOrWhiteSpace(body.Subject))
            return this.BadRequestEnvelope("subject_required");

        var ticketNumber = "T" + DateTime.UtcNow.ToString("yyMMddHHmmss");
        var complaint = new ComplaintEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UserId = CallerId,
            TicketNumber = ticketNumber,
            Type = "general",
            Title = body.Subject!.Trim(),
            Description = body.Body?.Trim() ?? "",
            Status = "open",
            Priority = "normal",
            Category = "support",
        };
        _db.Complaints.Add(complaint);
        await _db.SaveChangesAsync(ct);

        return this.OkEnvelope("support.ticket.create",
            new { id = complaint.Id.ToString() });
    }

    public sealed record ReplyBody(string? Body);

    [HttpPost("/support/tickets/{id}/replies")]
    public async Task<IActionResult> Reply(string id, [FromBody] ReplyBody body, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();
        if (!Guid.TryParse(id, out var cid)) return this.BadRequestEnvelope("invalid_id");
        if (string.IsNullOrWhiteSpace(body.Body)) return this.BadRequestEnvelope("body_required");

        var owns = await _db.Complaints.AsNoTracking()
            .AnyAsync(c => c.Id == cid && c.UserId == CallerId, ct);
        if (!owns) return this.ForbiddenEnvelope("not_owner");

        _db.ComplaintReplies.Add(new ComplaintReplyEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ComplaintId = cid,
            SenderId = CallerId,
            SenderName = "",
            IsStaff = false,
            Message = body.Body!.Trim(),
            IsInternal = false,
        });
        var ticket = await _db.Complaints.FirstAsync(c => c.Id == cid, ct);
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return this.OkEnvelope("support.ticket.reply", new { ok = true });
    }
}
