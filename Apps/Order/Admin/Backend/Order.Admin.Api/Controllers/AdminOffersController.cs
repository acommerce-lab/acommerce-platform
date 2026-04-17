using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Order.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Order.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/offers")]
[Authorize(Policy = "AdminOnly")]
public class AdminOffersController : ControllerBase
{
    private readonly IBaseAsyncRepository<Offer> _repo;
    private readonly OpEngine _engine;

    public AdminOffersController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo   = factory.CreateRepository<Offer>();
        _engine = engine;
    }

    /// <summary>
    /// GET /api/admin/offers?active=&amp;page=1&amp;pageSize=20
    /// قائمة العروض مع فلترة وترقيم.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // Frontend expects a flat list — return `.Items` directly.
        var result = await _repo.GetPagedAsync(
            pageNumber: page,
            pageSize: pageSize,
            predicate: o => active == null || o.IsActive == active,
            orderBy: o => o.CreatedAt,
            ascending: false);

        var rows = result.Items.Select(o => new
        {
            id          = o.Id,
            title       = o.Title,
            price       = o.Price,
            currency    = o.Currency,
            isAvailable = o.IsActive,
            category    = (string?)null,
            vendorName  = (string?)null
        }).ToList();

        return this.OkEnvelope("admin.offer.list", rows);
    }

    /// <summary>
    /// GET /api/admin/offers/{id}
    /// تفاصيل عرض.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var offer = await _repo.GetByIdAsync(id, ct);
        if (offer == null) return this.NotFoundEnvelope("offer_not_found");
        return this.OkEnvelope("admin.offer.get", offer);
    }

    /// <summary>
    /// POST /api/admin/offers/{id}/approve
    /// تفعيل عرض (تغيير IsActive إلى true).
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var offer = await _repo.GetByIdAsync(id, ct);
        if (offer == null) return this.NotFoundEnvelope("offer_not_found");

        if (offer.IsActive)
            return this.BadRequestEnvelope("offer_already_active", "العرض مفعّل بالفعل");

        var op = Entry.Create("admin.offer.approve")
            .Describe($"Admin approves Offer:{id}")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"Offer:{id}", 1, ("role", "offer"))
            .Tag("offer_id", id.ToString())
            .Tag("action", "approve")
            .Execute(async ctx =>
            {
                offer.IsActive = true;
                offer.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(offer, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("offer_approve_failed", result.ErrorMessage);

        return this.OkEnvelope("admin.offer.approve", new { offer.Id, offer.IsActive });
    }

    /// <summary>
    /// POST /api/admin/offers/{id}/reject
    /// رفض عرض (تغيير IsActive إلى false).
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectOfferRequest? req, CancellationToken ct)
    {
        var offer = await _repo.GetByIdAsync(id, ct);
        if (offer == null) return this.NotFoundEnvelope("offer_not_found");

        var op = Entry.Create("admin.offer.reject")
            .Describe($"Admin rejects Offer:{id}")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"Offer:{id}", 1, ("role", "offer"))
            .Tag("offer_id", id.ToString())
            .Tag("action", "reject")
            .Tag("reason", req?.Reason ?? "")
            .Execute(async ctx =>
            {
                offer.IsActive = false;
                offer.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(offer, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("offer_reject_failed", result.ErrorMessage);

        return this.OkEnvelope("admin.offer.reject", new { offer.Id, offer.IsActive });
    }

    /// <summary>
    /// POST /api/admin/offers/{id}/feature
    /// تبديل حالة التمييز (Featured) للعرض.
    /// </summary>
    [HttpPost("{id:guid}/feature")]
    public async Task<IActionResult> Feature(Guid id, CancellationToken ct)
    {
        var offer = await _repo.GetByIdAsync(id, ct);
        if (offer == null) return this.NotFoundEnvelope("offer_not_found");

        var op = Entry.Create("admin.offer.feature")
            .Describe($"Admin toggles featured for Offer:{id} (current: {offer.IsFeatured})")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"Offer:{id}", 1, ("role", "offer"))
            .Tag("offer_id", id.ToString())
            .Tag("action", "feature")
            .Execute(async ctx =>
            {
                offer.IsFeatured = !offer.IsFeatured;
                offer.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(offer, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("offer_feature_failed", result.ErrorMessage);

        return this.OkEnvelope("admin.offer.feature", new { offer.Id, offer.IsFeatured });
    }

    public record RejectOfferRequest(string? Reason);
}
