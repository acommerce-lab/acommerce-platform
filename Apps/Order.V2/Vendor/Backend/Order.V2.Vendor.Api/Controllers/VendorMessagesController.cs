using ACommerce.Chat.Operations;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Api.Entities;

namespace Order.V2.Vendor.Api.Controllers;

/// <summary>
/// Vendor-side chat: reads/writes the same Conversation/Message entities as
/// Order.V2.Api (shared DB), broadcasts on this backend's SignalR hub.
///
/// Routes live here under /api/messages/* so vendor frontend doesn't need to
/// reach across backends. Entity-level authorization ensures the caller is a
/// participant of the conversation.
/// </summary>
[ApiController]
[Authorize(Policy = "VendorOnly")]
[Route("api/messages")]
public class VendorMessagesController : ControllerBase
{
    private readonly IBaseAsyncRepository<Conversation> _convs;
    private readonly IBaseAsyncRepository<Order.V2.Api.Entities.Message> _msgs;
    private readonly OpEngine _engine;
    private readonly IChatService? _chat;
    private readonly IConnectionTracker? _connections;

    private static readonly TimeSpan ChatIdleTimeout = TimeSpan.FromMinutes(2);

    public VendorMessagesController(
        IRepositoryFactory f, OpEngine engine,
        IChatService? chat = null,
        IConnectionTracker? connections = null)
    {
        _convs       = f.CreateRepository<Conversation>();
        _msgs        = f.CreateRepository<Order.V2.Api.Entities.Message>();
        _engine      = engine;
        _chat        = chat;
        _connections = connections;
    }

    private string? CallerPartyId
    {
        get
        {
            var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                   ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return string.IsNullOrEmpty(sub) ? null : $"Vendor:{sub}";
        }
    }

    private Guid? CallerVendorId
    {
        get
        {
            var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                   ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var g) ? g : (Guid?)null;
        }
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> ListForVendor(CancellationToken ct)
    {
        if (CallerVendorId is not { } vendorId) return this.ForbiddenEnvelope("not_authenticated");
        var list = await _convs.GetAllWithPredicateAsync(c => c.VendorId == vendorId);
        var sorted = list
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Select(c => new { c.Id, c.CustomerId, c.LastMessageSnippet, c.LastMessageAt, Unread = c.UnreadVendorCount })
            .ToList();
        return this.OkEnvelope("conversation.list", sorted);
    }

    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<IActionResult> List(Guid id, CancellationToken ct)
    {
        var conv = await _convs.GetByIdAsync(id, ct);
        if (conv == null) return this.NotFoundEnvelope("conversation_not_found");
        if (CallerVendorId is not { } vid || conv.VendorId != vid)
            return this.ForbiddenEnvelope("not_a_participant");
        var list = await _msgs.GetAllWithPredicateAsync(m => m.ConversationId == id);
        return this.OkEnvelope("message.list", list.OrderBy(m => m.CreatedAt));
    }

    public record SendRequest(Guid ConversationId, string Content);

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendRequest req, CancellationToken ct)
    {
        if (CallerVendorId is not { } vendorId) return this.ForbiddenEnvelope("not_authenticated");
        var conv = await _convs.GetByIdAsync(req.ConversationId, ct);
        if (conv == null) return this.NotFoundEnvelope("conversation_not_found");
        if (conv.VendorId != vendorId) return this.ForbiddenEnvelope("not_a_participant");

        var msg = new Order.V2.Api.Entities.Message
        {
            Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            ConversationId = conv.Id, SenderId = vendorId, Content = req.Content
        };

        var op = Entry.Create("message.send")
            .Describe($"Vendor message in conversation {conv.Id}")
            .From($"Vendor:{vendorId}", 1, ("role", "sender"))
            .To($"User:{conv.CustomerId}", 1, ("role", "recipient"))
            .Tag("conversation_id", conv.Id.ToString())
            .Execute(async ctx =>
            {
                await _msgs.AddAsync(msg, ctx.CancellationToken);
                conv.LastMessageSnippet = req.Content.Length > 80 ? req.Content[..80] + "..." : req.Content;
                conv.LastMessageAt = DateTime.UtcNow;
                conv.UnreadCustomerCount++;
                await _convs.UpdateAsync(conv, ctx.CancellationToken);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, (IChatMessage)msg, ct);
        if (envelope.Operation.Status != "Success")
            return this.BadRequestEnvelope(envelope.Operation.FailedAnalyzer ?? "message_send_failed", envelope.Operation.ErrorMessage);

        if (_chat is not null) await _chat.BroadcastNewMessageAsync(msg, CancellationToken.None);
        return this.OkEnvelope("message.send", msg);
    }

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

    [HttpPost("/chat/{convId}/leave")]
    public async Task<IActionResult> LeaveChat(Guid convId, CancellationToken ct)
    {
        if (_chat is null || CallerPartyId is null) return this.OkEnvelope("chat.leave", new { ok = true });
        await _chat.LeaveConversationAsync(convId.ToString(), CallerPartyId, ct);
        return this.OkEnvelope("chat.leave", new { ok = true, conversationId = convId });
    }
}
