using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
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
/// <para>كُلّ المَسارات الكِتابِيَّة (Create + Reply) تَمُرّ عَبر
/// <see cref="OpEngine"/> + <c>SaveAtEnd</c> — لا <c>SaveChangesAsync</c>
/// مُباشِر، تَدقيق كامِل، وَ idempotency في حالَة إعادَة المُحاوَلَة.</para>
/// </summary>
[ApiController]
[Authorize]
public sealed class SupportController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    private readonly OpEngine          _engine;
    public SupportController(AshareV3DbContext db, OpEngine engine)
    {
        _db = db;
        _engine = engine;
    }

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
        var ticketId = Guid.NewGuid();
        var op = Entry.Create("support.ticket.create")
            .Describe($"User {CallerId} opens support ticket {ticketNumber}")
            .From($"User:{CallerId}",       1, ("role", "reporter"))
            .To($"Ticket:{ticketId}",       1, ("role", "created"))
            .Tag("user_id",       CallerId)
            .Tag("ticket_number", ticketNumber)
            .Tag("subject",       body.Subject!.Trim())
            .Execute(ctx =>
            {
                _db.Complaints.Add(new ComplaintEntity
                {
                    Id           = ticketId,
                    CreatedAt    = DateTime.UtcNow,
                    UserId       = CallerId,
                    TicketNumber = ticketNumber,
                    Type         = "general",
                    Title        = body.Subject!.Trim(),
                    Description  = body.Body?.Trim() ?? "",
                    Status       = "open",
                    Priority     = "normal",
                    Category     = "support",
                });
                return Task.CompletedTask;
            })
            .SaveAtEnd()
            .Build();
        var env = await _engine.ExecuteEnvelopeAsync(op, new { id = ticketId }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "create_failed",
                                           env.Operation.ErrorMessage);

        return this.OkEnvelope("support.ticket.create", new { id = ticketId.ToString() });
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

        var replyId = Guid.NewGuid();
        var op = Entry.Create("support.ticket.reply")
            .Describe($"User {CallerId} replies on ticket {cid}")
            .From($"User:{CallerId}",        1, ("role", "replier"))
            .To($"Ticket:{cid}",             1, ("role", "appended"))
            .Tag("ticket_id", cid.ToString())
            .Execute(async ctx =>
            {
                _db.ComplaintReplies.Add(new ComplaintReplyEntity
                {
                    Id          = replyId,
                    CreatedAt   = DateTime.UtcNow,
                    ComplaintId = cid,
                    SenderId    = CallerId,
                    SenderName  = "",
                    IsStaff     = false,
                    Message     = body.Body!.Trim(),
                    IsInternal  = false,
                });
                var ticket = await _db.Complaints.FirstAsync(c => c.Id == cid, ctx.CancellationToken);
                ticket.UpdatedAt = DateTime.UtcNow;
            })
            .SaveAtEnd()
            .Build();
        var env = await _engine.ExecuteEnvelopeAsync(op, new { id = replyId, ticketId = cid }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "reply_failed",
                                           env.Operation.ErrorMessage);

        return this.OkEnvelope("support.ticket.reply", new { ok = true });
    }
}
