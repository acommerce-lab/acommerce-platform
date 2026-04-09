using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Order.Api2.Entities;

namespace Order.Api2.Controllers;

[ApiController]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
    private readonly IBaseAsyncRepository<Conversation> _convs;
    private readonly IBaseAsyncRepository<Message> _msgs;
    private readonly IBaseAsyncRepository<Vendor> _vendors;
    private readonly IBaseAsyncRepository<User> _users;
    private readonly OpEngine _engine;

    public MessagesController(IRepositoryFactory f, OpEngine engine)
    {
        _convs = f.CreateRepository<Conversation>();
        _msgs = f.CreateRepository<Message>();
        _vendors = f.CreateRepository<Vendor>();
        _users = f.CreateRepository<User>();
        _engine = engine;
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
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CustomerId = req.CustomerId,
            VendorId = req.VendorId,
            OrderId = req.OrderId,
        };
        await _convs.AddAsync(conv, ct);
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
            {
                title = v.Name;
                emoji = v.LogoEmoji;
            }
            else if (users.TryGetValue(c.CustomerId, out var u))
            {
                title = u.FullName ?? u.PhoneNumber;
                emoji = "👤";
            }
            else
            {
                title = "(محذوف)";
                emoji = "💬";
            }
            return new
            {
                c.Id,
                Title = title,
                Emoji = emoji,
                c.LastMessageSnippet,
                c.LastMessageAt,
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
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ConversationId = conv.Id,
            SenderId = req.SenderId,
            Content = req.Content
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

        var envelope = await _engine.ExecuteEnvelopeAsync(op, msg, ct);
        if (envelope.Operation.Status != "Success") return BadRequest(envelope);
        return this.OkEnvelope("message.send", msg);
    }

    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<IActionResult> List(Guid id, CancellationToken ct)
    {
        var list = await _msgs.GetAllWithPredicateAsync(m => m.ConversationId == id);
        return this.OkEnvelope("message.list", list.OrderBy(m => m.CreatedAt));
    }
}
