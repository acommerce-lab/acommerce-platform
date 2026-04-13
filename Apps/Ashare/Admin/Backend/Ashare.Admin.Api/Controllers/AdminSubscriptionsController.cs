using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/subscriptions")]
[Authorize(Policy = "AdminOnly")]
public class AdminSubscriptionsController : ControllerBase
{
    private readonly IBaseAsyncRepository<Subscription> _repo;
    private readonly OpEngine _engine;

    public AdminSubscriptionsController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo   = factory.CreateRepository<Subscription>();
        _engine = engine;
    }

    /// <summary>
    /// GET /api/admin/subscriptions?status=&amp;page=1&amp;pageSize=20
    /// قائمة الاشتراكات مع فلترة وترقيم.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        SubscriptionStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SubscriptionStatus>(status, true, out var s))
            parsedStatus = s;

        var result = await _repo.GetPagedAsync(
            pageNumber: page,
            pageSize: pageSize,
            predicate: sub => parsedStatus == null || sub.Status == parsedStatus,
            orderBy: sub => sub.CreatedAt,
            ascending: false);

        return this.OkEnvelope("admin.subscription.list", result);
    }

    /// <summary>
    /// GET /api/admin/subscriptions/{id}
    /// تفاصيل اشتراك.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var sub = await _repo.GetByIdAsync(id, ct);
        if (sub == null) return this.NotFoundEnvelope("subscription_not_found");
        return this.OkEnvelope("admin.subscription.get", sub);
    }

    /// <summary>
    /// POST /api/admin/subscriptions/{id}/cancel
    /// إلغاء اشتراك.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var sub = await _repo.GetByIdAsync(id, ct);
        if (sub == null) return this.NotFoundEnvelope("subscription_not_found");

        if (sub.Status == SubscriptionStatus.Cancelled)
            return this.BadRequestEnvelope("subscription_already_cancelled", "الاشتراك ملغى بالفعل");

        var op = Entry.Create("admin.subscription.cancel")
            .Describe($"Admin cancels Subscription:{id} for User:{sub.UserId}")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"Subscription:{id}", 1, ("role", "subscription"))
            .Tag("subscription_id", id.ToString())
            .Tag("user_id", sub.UserId.ToString())
            .Tag("action", "cancel")
            .Execute(async ctx =>
            {
                sub.Status = SubscriptionStatus.Cancelled;
                sub.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(sub, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("subscription_cancel_failed", result.ErrorMessage);

        return this.OkEnvelope("admin.subscription.cancel", new { sub.Id, sub.Status });
    }
}
