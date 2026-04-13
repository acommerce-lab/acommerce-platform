using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/notifications")]
[Authorize(Policy = "AdminOnly")]
public class AdminNotificationsController : ControllerBase
{
    private readonly IBaseAsyncRepository<User> _users;
    private readonly IBaseAsyncRepository<Notification> _notifications;
    private readonly OpEngine _engine;

    public AdminNotificationsController(IRepositoryFactory factory, OpEngine engine)
    {
        _users         = factory.CreateRepository<User>();
        _notifications = factory.CreateRepository<Notification>();
        _engine        = engine;
    }

    /// <summary>
    /// POST /api/admin/notifications/broadcast
    /// إرسال إشعار جماعي لجميع المستخدمين أو شريحة محددة.
    /// الشرائح المدعومة: "all" (افتراضي)، "owners"، "customers"، "active"
    /// </summary>
    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastNotificationRequest req, CancellationToken ct)
    {
        // جلب المستخدمين المستهدفين
        var segment = req.Segment?.ToLowerInvariant() ?? "all";
        var allUsers = await _users.GetAllWithPredicateAsync(u => u.IsActive);

        IEnumerable<User> targetUsers = segment switch
        {
            "owners"    => allUsers.Where(u => u.Role == "owner"),
            "customers" => allUsers.Where(u => u.Role == "customer"),
            "active"    => allUsers,
            _           => allUsers   // "all" وأي قيمة أخرى
        };

        // إن كان userId محدد نرسل فقط إليه
        if (req.UserId.HasValue)
        {
            targetUsers = allUsers.Where(u => u.Id == req.UserId.Value);
        }

        var recipients = targetUsers.ToList();
        if (recipients.Count == 0)
            return this.BadRequestEnvelope("no_recipients", "لا توجد مستخدمون يطابقون الشريحة المحددة");

        var notificationList = new List<Notification>();

        var op = Entry.Create("admin.notification.broadcast")
            .Describe($"Admin broadcasts '{req.Title}' to {recipients.Count} users (segment: {segment})")
            .From($"Admin:system", recipients.Count, ("role", "admin"))
            .To($"Segment:{segment}", recipients.Count, ("role", "recipients"))
            .Tag("segment", segment)
            .Tag("recipients_count", recipients.Count.ToString())
            .Tag("notification_type", req.Type ?? "system")
            .Analyze(new RequiredFieldAnalyzer("title", () => req.Title))
            .Analyze(new RequiredFieldAnalyzer("body", () => req.Body))
            .Execute(async ctx =>
            {
                var now = DateTime.UtcNow;
                foreach (var user in recipients)
                {
                    var notification = new Notification
                    {
                        Id        = Guid.NewGuid(),
                        CreatedAt = now,
                        UserId    = user.Id,
                        Title     = req.Title,
                        Body      = req.Body,
                        Type      = req.Type ?? "system",
                        Priority  = req.Priority ?? "normal",
                        Channel   = req.Channel ?? "inapp",
                        ActionUrl = req.ActionUrl,
                        SentAt    = now,
                        DeliveryStatus = "sent"
                    };
                    notificationList.Add(notification);
                    await _notifications.AddAsync(notification, ctx.CancellationToken);
                }
                ctx.Set("recipientsCount", recipients.Count);
                ctx.Set("notificationsCreated", notificationList.Count);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, new
        {
            segment,
            recipientsCount = recipients.Count
        }, ct);

        if (envelope.Operation.Status != "Success")
            return this.BadRequestEnvelope("broadcast_failed", envelope.Operation.ErrorMessage);

        return this.OkEnvelope("admin.notification.broadcast", new
        {
            segment,
            recipientsCount = recipients.Count,
            notificationsCreated = notificationList.Count
        });
    }

    public record BroadcastNotificationRequest(
        string Title,
        string Body,
        string? Type,
        string? Priority,
        string? Channel,
        string? ActionUrl,
        string? Segment,
        Guid? UserId);
}
