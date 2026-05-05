using ACommerce.Kits.Support.Domain;
using ACommerce.Kits.Support.Operations;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ACommerce.Kits.Support.Backend;

/// <summary>
/// نقاط نهاية الدعم — مَعزولة تماماً عن Chat kit. كلّ تذكرة + رسائلها
/// تَعيش في Support كيانه الخاصّ، فلا تَظهر في قائمة المحادثات ولا
/// تُحَفِّز chat interceptors (FCM، broadcast، persistent-notif).
///
/// <para>Op type للردّ = <c>"ticket.reply"</c> — مُستقلّ عن
/// <c>"message.send"</c>. Push notifications لتذاكر الدعم تُضاف لاحقاً
/// كـ Support-specific bundle عند الحاجة.</para>
/// </summary>
[ApiController]
[Authorize(Policy = SupportKitPolicies.User)]
public sealed class SupportController : ControllerBase
{
    private readonly ISupportStore _store;
    private readonly OpEngine _engine;
    private readonly SupportKitOptions _options;

    public SupportController(
        ISupportStore store, OpEngine engine, SupportKitOptions options)
    {
        _store = store; _engine = engine; _options = options;
    }

    private string CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("user_id missing from token");

    private string CallerPartyId => $"{_options.PartyKind}:{CallerId}";

    [HttpGet("/support/tickets")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _store.ListForUserAsync(CallerId, ct);
        return this.OkEnvelope("ticket.list", rows);
    }

    [HttpGet("/support/tickets/{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        if (!await _store.CanAccessAsync(id, CallerId, ct))
            return this.ForbiddenEnvelope("not_a_participant");
        var t = await _store.GetAsync(id, ct);
        if (t is null) return this.NotFoundEnvelope("ticket_not_found");
        var msgs = await _store.GetMessagesAsync(id, ct);
        return this.OkEnvelope("ticket.details", new { ticket = t, messages = msgs });
    }

    public sealed record OpenRequest(string? Subject, string? Body, string? Priority, string? RelatedEntityId);

    [HttpPost("/support/tickets")]
    public async Task<IActionResult> Open([FromBody] OpenRequest req, CancellationToken ct)
    {
        var ticketId = Guid.NewGuid().ToString();
        var ticket = new InMemorySupportTicket(
            Id:              ticketId,
            UserId:          CallerId,
            ConversationId:  "",      // مَعزول: لا Conversation
            Subject:         req.Subject ?? "",
            Status:          "open",
            Priority:        req.Priority ?? "normal",
            RelatedEntityId: req.RelatedEntityId,
            AssignedAgentId: null,
            CreatedAt:       DateTime.UtcNow);

        var op = Entry.Create(SupportOps.TicketOpen)
            .Describe($"User {CallerId} opens a support ticket")
            .From(CallerPartyId, 1, ("role", "complainant"))
            .To($"Ticket:{ticketId}", 1, ("role", "created"))
            .Mark(SupportMarkers.IsTicketReply)
            .Tag("subject",   req.Subject ?? "")
            .Tag("priority",  req.Priority ?? "normal")
            .Tag(SupportTagKeys.TicketId, ticketId)
            .Analyze(new RequiredFieldAnalyzer("subject", () => req.Subject))
            .Analyze(new MaxLengthAnalyzer ("subject", () => req.Subject, _options.MaxSubjectLength))
            .Analyze(new RequiredFieldAnalyzer("body",    () => req.Body))
            .Analyze(new MaxLengthAnalyzer ("body",    () => req.Body, _options.MaxBodyLength))
            .Execute(async ctx =>
            {
                ctx.WithEntity<ISupportTicket>(ticket);
                await _store.AddNoSaveAsync(ticket, req.Body ?? "", ctx.CancellationToken);
            })
            .SaveAtEnd()
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, (object)ticket, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "open_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope(SupportOps.TicketOpen, ticket);
    }

    public sealed record ReplyRequest(string? Text);

    [HttpPost("/support/tickets/{id}/replies")]
    public async Task<IActionResult> Reply(string id, [FromBody] ReplyRequest req, CancellationToken ct)
    {
        if (!await _store.CanAccessAsync(id, CallerId, ct))
            return this.ForbiddenEnvelope("not_a_participant");

        var ticket = await _store.GetAsync(id, ct);
        if (ticket is null) return this.NotFoundEnvelope("ticket_not_found");

        var op = Entry.Create(SupportOps.TicketReply)
            .Describe($"User {CallerId} replies on ticket {id}")
            .From(CallerPartyId, 1, ("role", "sender"))
            .To($"Ticket:{id}",  1, ("role", "appended"))
            .Tag(SupportTagKeys.TicketId, id)
            .Mark(SupportMarkers.IsTicketReply)
            .Analyze(new RequiredFieldAnalyzer("text", () => req.Text))
            .Analyze(new MaxLengthAnalyzer ("text", () => req.Text, _options.MaxBodyLength))
            .Execute(async ctx =>
            {
                await _store.AppendMessageNoSaveAsync(id, CallerPartyId, req.Text ?? "", ctx.CancellationToken);
            })
            .SaveAtEnd()
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { id }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "reply_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope(SupportOps.TicketReply, new { ticketId = id });
    }

    public sealed record StatusRequest(string? Status);

    [HttpPatch("/support/tickets/{id}/status")]
    public async Task<IActionResult> SetStatus(string id, [FromBody] StatusRequest req, CancellationToken ct)
    {
        if (!await _store.CanAccessAsync(id, CallerId, ct))
            return this.ForbiddenEnvelope("not_a_participant");

        var ticket = await _store.GetAsync(id, ct);
        if (ticket is null) return this.NotFoundEnvelope("ticket_not_found");

        var newStatus = (req.Status ?? "").Trim().ToLowerInvariant();
        if (newStatus is not ("open" or "in_progress" or "resolved" or "closed"))
            return this.BadRequestEnvelope("invalid_status");

        var op = Entry.Create(SupportOps.TicketStatusChange)
            .Describe($"Ticket {id} status: {ticket.Status} → {newStatus}")
            .From(CallerPartyId, 1, ("role", "actor"))
            .To($"Ticket:{id}", 1, ("role", "status_updated"))
            .Mark(SupportMarkers.IsTicketReply)
            .Tag(SupportTagKeys.TicketId,   id)
            .Tag(SupportTagKeys.FromStatus, ticket.Status)
            .Tag(SupportTagKeys.ToStatus,   newStatus)
            .Execute(async ctx =>
            {
                await _store.SetStatusAsync(id, newStatus, ctx.CancellationToken);
            })
            .SaveAtEnd()
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { id, status = newStatus }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "status_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope(SupportOps.TicketStatusChange, new { id, status = newStatus });
    }
}
