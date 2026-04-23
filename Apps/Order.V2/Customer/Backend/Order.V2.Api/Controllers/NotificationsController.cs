using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Api.Entities;

namespace Order.V2.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IBaseAsyncRepository<Notification> _repo;
    private readonly OpEngine _engine;

    public NotificationsController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo = factory.CreateRepository<Notification>();
        _engine = engine;
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> ForUser(Guid userId, CancellationToken ct)
    {
        var list = await _repo.GetAllWithPredicateAsync(n => n.UserId == userId);
        return this.OkEnvelope("notification.list", list.OrderByDescending(n => n.CreatedAt));
    }

    public record SendRequest(Guid UserId, string Title, string Body, string? Type, string? Priority);

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendRequest req, CancellationToken ct)
    {
        var entity = new Notification
        {
            Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            UserId = req.UserId, Title = req.Title, Body = req.Body,
            Type = req.Type ?? "general", Priority = req.Priority ?? "normal",
            Channel = "inapp", DeliveryStatus = "sent", SentAt = DateTime.UtcNow
        };

        var op = Entry.Create("notify.send")
            .Describe($"Notify User:{req.UserId}: {req.Title}")
            .From("System", 1, ("role", "sender"))
            .To($"User:{req.UserId}", 1, ("role", "recipient"))
            .Tag("notification_type", req.Type ?? "general")
            .Execute(async ctx =>
            {
                await _repo.AddAsync(entity, ctx.CancellationToken);
                ctx.Set("notificationId", entity.Id);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, entity, ct);
        return this.OkEnvelope("notification.send", entity);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var n = await _repo.GetByIdAsync(id, ct);
        if (n == null) return this.NotFoundEnvelope();

        var op = Entry.Create("notification.read")
            .Describe($"User:{n.UserId} reads notification {id}")
            .From($"User:{n.UserId}", 1, ("role", "reader"))
            .To($"Notification:{id}", 1, ("role", "notification"))
            .Tag("notification_type", n.Type)
            .Execute(async ctx =>
            {
                n.IsRead = true;
                n.ReadAt = DateTime.UtcNow;
                await _repo.UpdateAsync(n, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("mark_read_failed", result.ErrorMessage);
        return this.OkEnvelope("notification.read", n);
    }

    [HttpPost("user/{userId:guid}/mark-all-read")]
    public async Task<IActionResult> MarkAll(Guid userId, CancellationToken ct)
    {
        var unread = await _repo.GetAllWithPredicateAsync(n => n.UserId == userId && !n.IsRead);

        var op = Entry.Create("notification.mark_all_read")
            .Describe($"User:{userId} marks {unread.Count} notifications as read")
            .From($"User:{userId}", unread.Count, ("role", "reader"))
            .To("System:notifications", unread.Count, ("role", "notification_batch"))
            .Tag("count", unread.Count.ToString())
            .Execute(async ctx =>
            {
                var now = DateTime.UtcNow;
                foreach (var n in unread)
                {
                    n.IsRead = true;
                    n.ReadAt = now;
                    await _repo.UpdateAsync(n, ctx.CancellationToken);
                }
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("mark_all_failed", result.ErrorMessage);
        return this.OkEnvelope("notification.mark_all_read", new { marked = unread.Count });
    }
}
