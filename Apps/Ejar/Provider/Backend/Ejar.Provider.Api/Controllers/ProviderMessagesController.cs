using ACommerce.Chat.Operations;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.Realtime.Operations.Abstractions;
using Ejar.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ejar.Provider.Api.Controllers;

/// <summary>
/// Provider-side chat: same conversation seed as Customer; the provider
/// participates as <c>PartnerId</c>. Reads the caller's conversations and
/// sends messages on this backend's hub. Chat enter/leave maps to the
/// realtime channel manager via <see cref="IChatService"/>.
/// </summary>
[ApiController]
[Authorize]
public class ProviderMessagesController : ControllerBase
{
    private readonly OpEngine _engine;
    private readonly IChatService? _chat;
    private readonly IConnectionTracker? _connections;
    private static readonly TimeSpan ChatIdleTimeout = TimeSpan.FromMinutes(2);

    public ProviderMessagesController(
        OpEngine engine, IChatService? chat = null, IConnectionTracker? connections = null)
    {
        _engine = engine; _chat = chat; _connections = connections;
    }

    private string CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("user_id missing");

    private string CallerPartyId => $"User:{CallerId}";

    // ─── Conversations the provider is part of ─────────────────────────────
    [HttpGet("/conversations")]
    public IActionResult ListMine()
    {
        var me = CallerId;
        var rows = EjarSeed.Conversations
            .Where(c => c.PartnerId == me)
            .OrderByDescending(c => c.LastAt)
            .Select(c => new {
                c.Id, c.PartnerName, c.PartnerId,
                c.ListingId, c.Subject, c.LastAt, c.UnreadCount,
                Last = c.Messages.LastOrDefault()?.Text
            }).ToList();
        return this.OkEnvelope("conversation.list", rows);
    }

    [HttpGet("/conversations/{id}")]
    public IActionResult Get(string id)
    {
        var c = EjarSeed.Conversations.FirstOrDefault(x => x.Id == id && x.PartnerId == CallerId);
        if (c is null) return this.NotFoundEnvelope("conversation_not_found");
        return this.OkEnvelope("conversation.details", new {
            c.Id, c.PartnerName, c.PartnerId, c.ListingId, c.Subject,
            Messages = c.Messages.Select(m => new { m.Id, From = m.From, Text = m.Text, m.SentAt }).ToList()
        });
    }

    public sealed record SendRequest(string? Text);

    [HttpPost("/conversations/{id}/messages")]
    public async Task<IActionResult> Send(string id, [FromBody] SendRequest req, CancellationToken ct)
    {
        var ix = EjarSeed.Conversations.FindIndex(c => c.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("conversation_not_found");
        var conv = EjarSeed.Conversations[ix];
        if (conv.PartnerId != CallerId) return this.ForbiddenEnvelope("not_a_participant");

        var msg = new EjarSeed.MessageSeed(
            $"M-{conv.Messages.Count + 1}", id, CallerId,
            req.Text ?? "", DateTime.UtcNow);

        var op = Entry.Create("message.send")
            .Describe($"Provider {CallerId} sends message in conversation {id}")
            .From($"Provider:{CallerId}", 1, ("role", "sender"))
            .To($"Conversation:{id}", 1, ("role", "appended"))
            .Tag("conversation_id", id)
            .Analyze(new RequiredFieldAnalyzer("text", () => req.Text))
            .Analyze(new MaxLengthAnalyzer("text",    () => req.Text, 4000))
            .Execute(ctx =>
            {
                conv.Messages.Add(msg);
                EjarSeed.Conversations[ix] = conv with { LastAt = msg.SentAt, UnreadCount = 0 };
                return Task.CompletedTask;
            })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, (IChatMessage)msg, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "send_failed", env.Operation.ErrorMessage);

        if (_chat is not null) await _chat.BroadcastNewMessageAsync(msg, CancellationToken.None);
        return this.OkEnvelope("message.send", msg);
    }

    // ─── Chat channel lifecycle ─────────────────────────────────────────────
    [HttpPost("/chat/{convId}/enter")]
    public async Task<IActionResult> Enter(string convId, CancellationToken ct)
    {
        if (_chat is null) return this.OkEnvelope("chat.enter", new { ok = true });
        var connId = _connections is null ? null : await _connections.GetConnectionIdAsync(CallerPartyId, ct);
        if (string.IsNullOrEmpty(connId))
            return this.OkEnvelope("chat.enter", new { ok = false, reason = "no_connection" });
        await _chat.EnterConversationAsync(convId, CallerPartyId, connId, ChatIdleTimeout, ct);
        return this.OkEnvelope("chat.enter", new { ok = true, conversationId = convId });
    }

    [HttpPost("/chat/{convId}/leave")]
    public async Task<IActionResult> Leave(string convId, CancellationToken ct)
    {
        if (_chat is not null) await _chat.LeaveConversationAsync(convId, CallerPartyId, ct);
        return this.OkEnvelope("chat.leave", new { ok = true, conversationId = convId });
    }
}
