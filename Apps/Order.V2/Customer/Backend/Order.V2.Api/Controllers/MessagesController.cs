using ACommerce.Chat.Operations;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Api.Entities;

namespace Order.V2.Api.Controllers;

[ApiController]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
    private readonly IBaseAsyncRepository<Conversation> _convs;
    private readonly IBaseAsyncRepository<Message> _msgs;
    private readonly IBaseAsyncRepository<Vendor> _vendors;
    private readonly IBaseAsyncRepository<User> _users;
    private readonly OpEngine _engine;
    private readonly IChatService? _chat;
    private readonly IConnectionTracker? _connections;

    /// <summary>
    /// Chat channel idle timeout — backend auto-closes a user's chat:conv:X
    /// subscription after this, which re-opens notif:conv:X for them.
    /// </summary>
    private static readonly TimeSpan ChatIdleTimeout = TimeSpan.FromMinutes(2);

    public MessagesController(
        IRepositoryFactory f,
        OpEngine engine,
        IChatService? chat = null,
        IConnectionTracker? connections = null)
    {
        _convs       = f.CreateRepository<Conversation>();
        _msgs        = f.CreateRepository<Message>();
        _vendors     = f.CreateRepository<Vendor>();
        _users       = f.CreateRepository<User>();
        _engine      = engine;
        _chat        = chat;
        _connections = connections;
    }

    public record StartRequest(Guid CustomerId, Guid VendorId, Guid? OrderId);

    [HttpPost("conversations")]
    public async Task<IActionResult> Start([FromBody] StartRequest req, CancellationToken ct)
    {
        var existing = await _convs.GetAllWithPredicateAsync(
            c => c.CustomerId == req.CustomerId && c.VendorId == req.VendorId);
        if (existing.Count > 0)
            return this.OkEnvelope("conversation.get", existing.First());

        var conv = new Conversation
        {
            Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            CustomerId = req.CustomerId, VendorId = req.VendorId, OrderId = req.OrderId,
        };

        var op = Entry.Create("conversation.start")
            .Describe($"User:{req.CustomerId} starts conversation with Vendor:{req.VendorId}")
            .From($"User:{req.CustomerId}", 1, ("role", "customer"))
            .To($"Vendor:{req.VendorId}", 1, ("role", "vendor"))
            .Tag("conversation_id", conv.Id.ToString())
            .Execute(async ctx =>
            {
                await _convs.AddAsync(conv, ctx.CancellationToken);
                ctx.Set("conversationId", conv.Id);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("conversation_start_failed", result.ErrorMessage);
        return this.OkEnvelope("conversation.create", conv);
    }

    [HttpGet("conversations/by-user/{userId:guid}")]
    public async Task<IActionResult> ListByUser(Guid userId, CancellationToken ct)
    {
        var list = await _convs.GetAllWithPredicateAsync(
            c => c.CustomerId == userId || c.VendorId == userId);
        var sorted = list.OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt).ToList();
        var vendors = (await _vendors.ListAllAsync(ct)).ToDictionary(v => v.Id);
        var users = (await _users.ListAllAsync(ct)).ToDictionary(u => u.Id);
        var result = sorted.Select(c =>
        {
            var isCustomerView = c.CustomerId == userId;
            string title;
            string emoji;
            if (isCustomerView && vendors.TryGetValue(c.VendorId, out var v))
            { title = v.Name; emoji = v.LogoEmoji; }
            else if (users.TryGetValue(c.CustomerId, out var u))
            { title = u.FullName ?? u.PhoneNumber; emoji = "👤"; }
            else
            { title = "(محذوف)"; emoji = "💬"; }
            return new
            {
                c.Id, Title = title, Emoji = emoji,
                c.LastMessageSnippet, c.LastMessageAt,
                Unread = isCustomerView ? c.UnreadCustomerCount : c.UnreadVendorCount
            };
        }).ToList();
        return this.OkEnvelope("conversation.list", result);
    }

    public record SendRequest(Guid ConversationId, Guid SenderId, string Content);

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendRequest req, CancellationToken ct)
    {
        var conv = await _convs.GetByIdAsync(req.ConversationId, ct);
        if (conv == null) return this.NotFoundEnvelope("conversation_not_found");
        if (req.SenderId != conv.CustomerId && req.SenderId != conv.VendorId)
            return this.ForbiddenEnvelope("not_a_participant");

        var msg = new Message
        {
            Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            ConversationId = conv.Id, SenderId = req.SenderId, Content = req.Content
        };

        var recipient = req.SenderId == conv.CustomerId ? conv.VendorId : conv.CustomerId;

        var op = Entry.Create("message.send")
            .Describe($"Message in conversation {conv.Id}")
            .From($"User:{req.SenderId}", 1, ("role", "sender"))
            .To($"User:{recipient}", 1, ("role", "recipient"))
            .Tag("conversation_id", conv.Id.ToString())
            .Execute(async ctx =>
            {
                await _msgs.AddAsync(msg, ctx.CancellationToken);
                conv.LastMessageSnippet = req.Content.Length > 80 ? req.Content[..80] + "..." : req.Content;
                conv.LastMessageAt = DateTime.UtcNow;
                if (req.SenderId == conv.CustomerId) conv.UnreadVendorCount++;
                else conv.UnreadCustomerCount++;
                await _convs.UpdateAsync(conv, ctx.CancellationToken);
                ctx.Set("messageId", msg.Id);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, (IChatMessage)msg, ct);
        if (envelope.Operation.Status != "Success")
            return this.BadRequestEnvelope(envelope.Operation.FailedAnalyzer ?? "message_send_failed", envelope.Operation.ErrorMessage);

        // Broadcast on both chat:conv:X and notif:conv:X groups; recipient sees
        // the message in-chat if they're in the conversation, or as notif otherwise.
        if (_chat is not null) await _chat.BroadcastNewMessageAsync(msg, CancellationToken.None);

        return this.OkEnvelope("message.send", msg);
    }

    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<IActionResult> List(Guid id, CancellationToken ct)
    {
        var list = await _msgs.GetAllWithPredicateAsync(m => m.ConversationId == id);
        return this.OkEnvelope("message.list", list.OrderBy(m => m.CreatedAt));
    }

    public record MarkReadRequest(Guid ReaderId);

    [HttpPost("conversations/{id:guid}/mark-read")]
    public async Task<IActionResult> MarkRead(Guid id, [FromBody] MarkReadRequest req, CancellationToken ct)
    {
        var conv = await _convs.GetByIdAsync(id, ct);
        if (conv == null) return this.NotFoundEnvelope("conversation_not_found");
        if (req.ReaderId != conv.CustomerId && req.ReaderId != conv.VendorId)
            return this.ForbiddenEnvelope("not_a_participant");

        var op = Entry.Create("conversation.mark_read")
            .Describe($"User:{req.ReaderId} marks conversation {id} as read")
            .From($"User:{req.ReaderId}", 1, ("role", "reader"))
            .To($"Conversation:{id}", 1, ("role", "conversation"))
            .Tag("conversation_id", id.ToString())
            .Execute(async ctx =>
            {
                if (req.ReaderId == conv.CustomerId) conv.UnreadCustomerCount = 0;
                else conv.UnreadVendorCount = 0;
                await _convs.UpdateAsync(conv, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("mark_read_failed", result.ErrorMessage);
        return this.OkEnvelope("conversation.mark_read", new { });
    }

    // ─── Chat channel lifecycle ──────────────────────────────────────────────
    // Caller identity is resolved from the JWT `sub` claim (JwtRegisteredClaimNames.Sub).

    private string? CallerPartyId
    {
        get
        {
            var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                   ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return string.IsNullOrEmpty(sub) ? null : $"User:{sub}";
        }
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpPost("/chat/{convId}/enter")]
    public async Task<IActionResult> EnterChat(Guid convId, CancellationToken ct)
    {
        if (_chat is null) return this.OkEnvelope("chat.enter", new { ok = true });
        if (CallerPartyId is null) return this.ForbiddenEnvelope("not_authenticated");
        var connId = _connections is null ? null : await _connections.GetConnectionIdAsync(CallerPartyId, ct);
        if (string.IsNullOrEmpty(connId))
            return this.OkEnvelope("chat.enter", new { ok = false, reason = "no_connection" });
        await _chat.EnterConversationAsync(convId.ToString(), CallerPartyId, connId, ChatIdleTimeout, ct);
        return this.OkEnvelope("chat.enter", new { ok = true, conversationId = convId });
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpPost("/chat/{convId}/leave")]
    public async Task<IActionResult> LeaveChat(Guid convId, CancellationToken ct)
    {
        if (_chat is null || CallerPartyId is null) return this.OkEnvelope("chat.leave", new { ok = true });
        await _chat.LeaveConversationAsync(convId.ToString(), CallerPartyId, ct);
        return this.OkEnvelope("chat.leave", new { ok = true, conversationId = convId });
    }
}
