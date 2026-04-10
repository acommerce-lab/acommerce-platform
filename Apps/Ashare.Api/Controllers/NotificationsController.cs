using ACommerce.Notification.Operations.Abstractions;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using NotificationEntity = Ashare.Api.Entities.Notification;

namespace Ashare.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IBaseAsyncRepository<NotificationEntity> _repo;
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly OpEngine _engine;

    public NotificationsController(
        IRepositoryFactory factory,
        IEnumerable<INotificationChannel> channels,
        OpEngine engine)
    {
        _repo = factory.CreateRepository<NotificationEntity>();
        _channels = channels;
        _engine = engine;
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> ForUser(Guid userId, CancellationToken ct)
    {
        var list = await _repo.GetAllWithPredicateAsync(n => n.UserId == userId);
        return this.OkEnvelope("notification.list", list.OrderByDescending(n => n.CreatedAt).ToList());
    }

    [HttpGet("user/{userId:guid}/unread-count")]
    public async Task<IActionResult> UnreadCount(Guid userId, CancellationToken ct)
    {
        var count = await _repo.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken: ct);
        return this.OkEnvelope("notification.unread_count", new { unreadCount = count });
    }

    public record SendNotificationRequest(
        Guid UserId,
        string Title,
        string Body,
        string Channel,        // "inapp", "firebase", ...
        string? Type,
        string? Priority,
        string? ActionUrl);

    /// <summary>
    /// إرسال إشعار - يستخدم القنوات المسجلة والـ OperationEngine لتسجيل القيد.
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendNotificationRequest req, CancellationToken ct)
    {
        var channel = _channels.FirstOrDefault(c => c.ChannelName == req.Channel);
        if (channel == null)
            return this.BadRequestEnvelope(
                "channel_not_registered",
                $"channel '{req.Channel}' not found",
                $"available: {string.Join(", ", _channels.Select(c => c.ChannelName))}");

        // إنشاء الكيان أولاً
        var entity = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UserId = req.UserId,
            Title = req.Title,
            Body = req.Body,
            Type = req.Type ?? "info",
            Priority = req.Priority ?? "normal",
            Channel = req.Channel,
            ActionUrl = req.ActionUrl,
            DeliveryStatus = "pending"
        };
        await _repo.AddAsync(entity, ct);

        // === العملية المحاسبية ===
        // النظام (مدين) ← المستخدم (دائن) برسالة على قناة محددة
        var op = Entry.Create("notify.send")
            .Describe($"Notify User:{req.UserId}: {req.Title}")
            .From("System", 1, ("role", "sender"))
            .To($"User:{req.UserId}", 1, ("role", "recipient"), ("delivery", "pending"))
            .Tag("channel", req.Channel)
            .Tag("notification_type", req.Type ?? "info")
            .Tag("priority", req.Priority ?? "normal")
            .Tag("notification_id", entity.Id.ToString())
            .Execute(async ctx =>
            {
                var sent = await channel.SendAsync(req.UserId.ToString(), req.Title, req.Body, null, ctx.CancellationToken);

                // تعديل بيانات الإشعار حسب نتيجة العملية
                entity.DeliveryStatus = sent ? "sent" : "failed";
                entity.SentAt = sent ? DateTime.UtcNow : null;
                if (!sent) entity.FailureReason = "channel_send_failed";
                await _repo.UpdateAsync(entity, ctx.CancellationToken);

                ctx.Set("sent", sent);

                // تحديث الطرف المُستلم بحالة التسليم
                var recipient = ctx.Operation.GetPartiesByTag("role", "recipient").FirstOrDefault();
                if (recipient != null)
                {
                    recipient.RemoveTag("delivery");
                    recipient.AddTag("delivery", sent ? "sent" : "failed");
                }
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, entity, ct);
        return Ok(envelope);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var n = await _repo.GetByIdAsync(id, ct);
        if (n == null) return this.NotFoundEnvelope("notification_not_found");
        n.IsRead = true;
        n.ReadAt = DateTime.UtcNow;

        var op = Entry.Create("notification.read")
            .Describe($"Mark notification {id} as read for User:{n.UserId}")
            .From($"User:{n.UserId}", 1, ("role", "reader"))
            .To($"Notification:{id}", 1, ("role", "notification"))
            .Tag("notification_id", id.ToString())
            .Execute(async ctx =>
            {
                await _repo.UpdateAsync(n, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("notification_read_failed", result.ErrorMessage);

        return this.OkEnvelope("notification.read", n);
    }

    [HttpPost("user/{userId:guid}/mark-all-read")]
    public async Task<IActionResult> MarkAllRead(Guid userId, CancellationToken ct)
    {
        var unread = await _repo.GetAllWithPredicateAsync(n => n.UserId == userId && !n.IsRead);
        var now = DateTime.UtcNow;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }

        var op = Entry.Create("notification.mark_all_read")
            .Describe($"Mark all {unread.Count} notifications as read for User:{userId}")
            .From($"User:{userId}", unread.Count, ("role", "reader"))
            .To($"Notifications:batch", unread.Count, ("role", "notifications"))
            .Tag("user_id", userId.ToString())
            .Tag("count", unread.Count.ToString())
            .Execute(async ctx =>
            {
                foreach (var n in unread)
                {
                    await _repo.UpdateAsync(n, ctx.CancellationToken);
                }
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("notification_mark_all_read_failed", result.ErrorMessage);

        return this.OkEnvelope("notification.mark_all_read", new { markedCount = unread.Count });
    }
}
