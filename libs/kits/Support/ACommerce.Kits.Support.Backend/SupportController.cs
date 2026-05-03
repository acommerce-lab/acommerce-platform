using ACommerce.Chat.Operations;
using ACommerce.Kits.Chat.Backend;
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
/// نقاط نهاية الدعم الموحَّدة. كلّ تذكرة = محادثة (Chat kit) + metadata.
/// لا يحوي هذا الـ controller منطق رسائل أو broadcast — كلّه يمرّ على
/// <see cref="IChatStore.AppendMessageAsync"/> داخل <c>Execute</c> body
/// للعمليّة (envelope) فيرث realtime + DB notification + FCM push من
/// <c>EjarCustomerChatStore</c> (أو ما يكافئه في تطبيقات أخرى).
///
/// <para>كلّ side effect يحدث <b>داخل</b> الـ envelope — لا استدعاء
/// خارجه. هذا يضمن أنّ أيّ <c>IOperationInterceptor</c> مسجَّل لاحقاً
/// (مثلاً audit، rate-limit، spam-detection) يلتقطه تلقائياً.</para>
///
/// <para>المسارات:
///   <c>GET    /support/tickets</c>         — قائمة تذاكر المستخدم.
///   <c>GET    /support/tickets/{id}</c>    — تذكرة + قائمة رسائل المحادثة.
///   <c>POST   /support/tickets</c>          — فتح تذكرة جديدة.
///   <c>POST   /support/tickets/{id}/replies</c> — ردّ على تذكرة.
///   <c>PATCH  /support/tickets/{id}/status</c>  — تغيير الحالة (مستقبليّ: للوكيل فقط).
/// </para>
/// </summary>
[ApiController]
[Authorize(Policy = SupportKitPolicies.User)]
public sealed class SupportController : ControllerBase
{
    private readonly ISupportStore _store;
    private readonly IChatStore _chat;
    private readonly OpEngine _engine;
    private readonly SupportKitOptions _options;

    public SupportController(
        ISupportStore store, IChatStore chat, OpEngine engine, SupportKitOptions options)
    {
        _store = store; _chat = chat; _engine = engine; _options = options;
    }

    private string CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("user_id missing from token");

    private string CallerPartyId => $"{_options.PartyKind}:{CallerId}";

    // ─── GET /support/tickets ──────────────────────────────────────────
    [HttpGet("/support/tickets")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _store.ListForUserAsync(CallerId, ct);
        return this.OkEnvelope("ticket.list", rows);
    }

    // ─── GET /support/tickets/{id} ─────────────────────────────────────
    [HttpGet("/support/tickets/{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        if (!await _store.CanAccessAsync(id, CallerId, ct))
            return this.ForbiddenEnvelope("not_a_participant");
        var t = await _store.GetAsync(id, ct);
        if (t is null) return this.NotFoundEnvelope("ticket_not_found");
        var msgs = await _chat.GetMessagesAsync(t.ConversationId, ct);
        return this.OkEnvelope("ticket.details", new { ticket = t, messages = msgs });
    }

    // ─── POST /support/tickets ─────────────────────────────────────────
    public sealed record OpenRequest(string? Subject, string? Body, string? Priority, string? RelatedEntityId);

    [HttpPost("/support/tickets")]
    public async Task<IActionResult> Open([FromBody] OpenRequest req, CancellationToken ct)
    {
        // التذكرة كحدث OAM أصيل: نبنيها كـ POCO (InMemorySupportTicket)
        // مع Conversation Id مُسبَق العشوائيّ، ونضعها على
        // ctx.WithEntity<ISupportTicket>(). الـ store يُستدعى عبر
        // AddNoSaveAsync (default no-op): persistence اختياريّ — العمليّة
        // تنجح كحدث OAM حتّى دون جداول SupportTickets/Conversations.
        var ticketId = Guid.NewGuid().ToString();
        var convId   = Guid.NewGuid().ToString();
        var ticket = new InMemorySupportTicket(
            Id:              ticketId,
            UserId:          CallerId,
            ConversationId:  convId,
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
            .Tag(SupportTagKeys.TicketId,  ticketId)
            .Tag("conversation_id",        convId)
            .Analyze(new RequiredFieldAnalyzer("subject", () => req.Subject))
            .Analyze(new MaxLengthAnalyzer ("subject", () => req.Subject, _options.MaxSubjectLength))
            .Analyze(new RequiredFieldAnalyzer("body",    () => req.Body))
            .Analyze(new MaxLengthAnalyzer ("body",    () => req.Body, _options.MaxBodyLength))
            .Execute(async ctx =>
            {
                ctx.WithEntity<ISupportTicket>(ticket);
                await _store.AddNoSaveAsync(ticket, req.Body ?? "", ctx.CancellationToken);
            })
            .SaveAtEnd()  // F6: Conversation + Ticket + initial-message ذرّيّاً
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, (object)ticket, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "open_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope(SupportOps.TicketOpen, ticket);
    }

    // ─── POST /support/tickets/{id}/replies ────────────────────────────
    public sealed record ReplyRequest(string? Text);

    [HttpPost("/support/tickets/{id}/replies")]
    public async Task<IActionResult> Reply(string id, [FromBody] ReplyRequest req, CancellationToken ct)
    {
        if (!await _store.CanAccessAsync(id, CallerId, ct))
            return this.ForbiddenEnvelope("not_a_participant");

        var ticket = await _store.GetAsync(id, ct);
        if (ticket is null) return this.NotFoundEnvelope("ticket_not_found");

        // OAM Type = "message.send" (نفس Chat kit) — الردّ يُورِّث أيّ
        // interceptor مسجَّل على رسائل الدردشة (realtime، FCM، …) دون
        // كود إضافيّ. التمييز للـ Support interceptors عبر marker.
        //
        // H3: الرسالة كحدث OAM أصيل — POCO ينسرب لـ ctx.WithEntity ويفصل
        // عن persistence. الـ store يُستدعى عبر AppendNoSaveAsync (default
        // no-op)، فإسقاطه أو تعطيله لا يكسر الردّ.
        var msg = new InMemoryChatMessage(
            Id:             Guid.NewGuid().ToString(),
            ConversationId: ticket.ConversationId,
            SenderPartyId:  CallerPartyId,
            Body:           req.Text ?? "",
            SentAt:         DateTime.UtcNow);

        var op = Entry.Create(SupportOps.TicketReply)
            .Describe($"User {CallerId} replies on ticket {id}")
            .From(CallerPartyId, 1, ("role", "sender"))
            .To($"Conversation:{ticket.ConversationId}", 1, ("role", "appended"))
            .Tag("conversation_id",   ticket.ConversationId)
            .Mark(SupportMarkers.IsTicketReply)
            .Tag(SupportTagKeys.TicketId, id)
            .Analyze(new RequiredFieldAnalyzer("text", () => req.Text))
            .Analyze(new MaxLengthAnalyzer ("text", () => req.Text, _options.MaxBodyLength))
            .Execute(async ctx =>
            {
                ctx.WithEntity<IChatMessage>(msg);
                await _chat.AppendNoSaveAsync(msg, ctx.CancellationToken);
            })
            .SaveAtEnd()  // F6: حفظ ذرّيّ للرسالة + Conversation update
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, (object)msg, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "reply_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope(SupportOps.TicketReply, msg);
    }

    // ─── PATCH /support/tickets/{id}/status ────────────────────────────
    public sealed record StatusRequest(string? Status);

    [HttpPatch("/support/tickets/{id}/status")]
    public async Task<IActionResult> SetStatus(string id, [FromBody] StatusRequest req, CancellationToken ct)
    {
        // ملاحظة: حالياً المستخدم نفسه يستطيع إغلاق تذكرته. لاحقاً نُضيف
        // role check للوكلاء/الإدارة عبر [Authorize(Roles="agent,admin")]
        // على endpoint منفصل أو شرط داخل العمليّة.
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
            .SaveAtEnd()  // F6: تحديث Ticket.Status + رسالة النظام في حفظ واحد
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { id, status = newStatus }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "status_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope(SupportOps.TicketStatusChange, new { id, status = newStatus });
    }
}
