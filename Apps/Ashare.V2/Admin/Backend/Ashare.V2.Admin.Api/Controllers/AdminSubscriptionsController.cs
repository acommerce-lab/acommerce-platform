using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.V2.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.V2.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/subscriptions")]
[Authorize(Policy = "AdminOnly")]
public class AdminSubscriptionsController : ControllerBase
{
    private readonly IBaseAsyncRepository<Subscription> _repo;

    public AdminSubscriptionsController(IRepositoryFactory factory) =>
        _repo = factory.CreateRepository<Subscription>();

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _repo.GetPagedAsync(
            pageNumber: page, pageSize: pageSize,
            predicate: s => status == null || s.Status == status,
            orderBy: s => s.CreatedAt, ascending: false);

        var rows = result.Items.Select(s => new
        {
            id          = s.Id,
            ownerId     = s.OwnerId,
            planKey     = s.PlanKey,
            listingsLimit = s.ListingsLimit,
            periodStart = s.PeriodStart,
            periodEnd   = s.PeriodEnd,
            status      = s.Status,
            isActive    = s.Status == "active" && s.PeriodEnd > DateTime.UtcNow,
            createdAt   = s.CreatedAt
        });
        return this.OkEnvelope("admin.subscription.list", rows);
    }
}
