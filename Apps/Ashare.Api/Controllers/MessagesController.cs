using ACommerce.OperationEngine.Analyzers;
using ACommerce.Subscriptions.Operations;
using Ashare.Api.Services;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Api.Controllers;

[ApiController]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
    private readonly IBaseAsyncRepository<Conversation> _convs;
    private readonly IBaseAsyncRepository<Message> _msgs;
    private readonly IBaseAsyncRepository<Listing> _listings;
    private readonly IRealtimeTransport _transport;
    private readonly OpEngine _engine;

    public MessagesController(
        IRepositoryFactory factory,
        IRealtimeTransport transport,
        OpEngine engine)
    {
        _convs = factory.CreateRepository<Conversation>();
        _msgs = factory.CreateRepository<Message>();
        _listings = factory.CreateRepository<Listing>();
        _transport = transport;
        _engine = engine;
    }

    public record StartConversationRequest(Guid ListingId, Guid CustomerId);

    [HttpPost("conversations")]
    public async Task<IActionResult> StartConversation([FromBody] StartConversationRequest req, CancellationToken ct)
    {
        var listing = await _listings.GetByIdAsync(req.ListingId, ct);
        if (listing == null) return this.NotFoundEnvelope("listing_not_found");

        if (!listing.IsMessagingAllowed)
            return this.BadRequestEnvelope("messaging_not_allowed_for_listing");

        var existing = await _convs.GetAllWithPredicateAsync(c =>
            c.ListingId == req.ListingId &&
            c.CustomerId == req.CustomerId &&
            c.OwnerId == listing.OwnerId);

        if (existing.Count > 0)
            return this.OkEnvelope("conversation.get", existing.First());

        var conv = new Conversation
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ListingId = req.ListingId,
            CustomerId = req.CustomerId,
            OwnerId = listing.OwnerId
        };
        await _convs.AddAsync(conv, ct);
        return this.OkEnvelope("conversation.create", conv);
    }

    [HttpGet("conversations/{id:guid}")]
    public async Task<IActionResult> GetConversation(Guid id, CancellationToken ct)
    {
        var c = await _convs.GetByIdAsync(id, ct);
        return c == null ? this.NotFoundEnvelope("conversation_not_found") : this.OkEnvelope("conversation.get", c);
    }

    [HttpGet("conversations/by-user/{userId:guid}")]
    public async Task<IActionResult> ListByUser(Guid userId, CancellationToken ct)
    {
        var list = await _convs.GetAllWithPredicateAsync(c =>
            c.CustomerId == userId || c.OwnerId == userId);
        return this.OkEnvelope("conversation.list", list.OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt).ToList());
    }

    public record SendMessageRequest(Guid ConversationId, Guid SenderId, string Content, string? MessageType);

    /// <summary>
    /// إرسال رسالة - قيد محاسبي:
    /// المُرسل (مدين) ← المُستقبل (دائن) برسالة، ثم بث عبر الزمن الحقيقي.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest req, CancellationToken ct)
    {
        var conv = await _convs.GetByIdAsync(req.ConversationId, ct);
        if (conv == null) return this.NotFoundEnvelope("conversation_not_found");

        if (req.SenderId != conv.CustomerId && req.SenderId != conv.OwnerId)
            return this.ForbiddenEnvelope("not_a_participant");

        var recipient = req.SenderId == conv.CustomerId ? conv.OwnerId : conv.CustomerId;

        var msg = new Message
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ConversationId = conv.Id,
            SenderId = req.SenderId,
            Content = req.Content,
            MessageType = req.MessageType ?? "text"
        };

        // قيد بسيط - المعترضات المحقونة من الـ registry تتدخّل تلقائياً
        var builder = Entry.Create(AshareOps.ChatSend)
            .Describe($"Message from User:{req.SenderId} to User:{recipient}")
            .From($"User:{req.SenderId}", 1, ("role", AshareRoles.Sender.Name))
            .To($"User:{recipient}", 1, ("role", AshareRoles.Recipient.Name), ("delivery", "pending"))
            .Tag(AshareTags.ConversationId, conv.Id)
            .Tag(AshareTags.MessageType, msg.MessageType)
            .Analyze(new RequiredFieldAnalyzer("content", () => req.Content));

        // إذا كان المُرسل مالك العرض → حصة على الرسائل (المعترض يتدخّل تلقائياً)
        if (req.SenderId == conv.OwnerId)
        {
            builder.Tag(QuotaTagKeys.Check, QuotaCheckKinds.MessagesSend);
            builder.Tag(QuotaTagKeys.UserId, req.SenderId);
        }

        var op = builder
            .Execute(async ctx =>
            {
                // حفظ الرسالة وتحديث المحادثة
                await _msgs.AddAsync(msg, ctx.CancellationToken);

                conv.LastMessageSnippet = req.Content.Length > 80 ? req.Content[..80] + "..." : req.Content;
                conv.LastMessageAt = DateTime.UtcNow;
                if (req.SenderId == conv.CustomerId) conv.UnreadOwnerCount++;
                else conv.UnreadCustomerCount++;
                await _convs.UpdateAsync(conv, ctx.CancellationToken);

                // بث عبر الزمن الحقيقي للمستقبل
                await _transport.SendToUserAsync(
                    recipient.ToString(),
                    "MessageReceived",
                    new { conversationId = conv.Id, message = msg },
                    ctx.CancellationToken);

                ctx.Set("messageId", msg.Id);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, msg, ct);

        if (envelope.Operation.Status != "Success")
            return BadRequest(envelope);

        return Created($"/api/messages/{msg.Id}", envelope);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetMessage(Guid id, CancellationToken ct)
    {
        var m = await _msgs.GetByIdAsync(id, ct);
        return m == null ? this.NotFoundEnvelope("message_not_found") : this.OkEnvelope("message.get", m);
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> ListInConversation(Guid conversationId, CancellationToken ct)
    {
        var list = await _msgs.GetAllWithPredicateAsync(m => m.ConversationId == conversationId);
        return this.OkEnvelope("message.list", list.OrderBy(m => m.CreatedAt).ToList());
    }
}
