using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/listings")]
[Authorize(Policy = "AdminOnly")]
public class AdminListingsController : ControllerBase
{
    private readonly IBaseAsyncRepository<Listing> _repo;
    private readonly OpEngine _engine;

    public AdminListingsController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo   = factory.CreateRepository<Listing>();
        _engine = engine;
    }

    /// <summary>
    /// GET /api/admin/listings?status=&amp;city=&amp;page=1&amp;pageSize=20
    /// قائمة الإعلانات مع فلترة وترقيم.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? city,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        ListingStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ListingStatus>(status, true, out var s))
            parsedStatus = s;

        // Frontend expects a flat list — return `.Items` directly.
        var result = await _repo.GetPagedAsync(
            pageNumber: page,
            pageSize: pageSize,
            predicate: l =>
                (parsedStatus == null || l.Status == parsedStatus) &&
                (city == null || l.City == city),
            orderBy: l => l.CreatedAt,
            ascending: false);

        var rows = result.Items.Select(l => new
        {
            id             = l.Id,
            title          = l.Title,
            status         = (int)l.Status,
            approvalStatus = l.Status == ListingStatus.Published ? "approved"
                           : l.Status == ListingStatus.Rejected  ? "rejected"
                           : "pending",
            categoryName   = l.City,
            bookingCount   = 0,
            totalRevenue   = 0m,
            createdAt      = l.CreatedAt
        }).ToList();

        return this.OkEnvelope("admin.listing.list", rows);
    }

    /// <summary>
    /// GET /api/admin/listings/{id}
    /// تفاصيل إعلان.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var listing = await _repo.GetByIdAsync(id, ct);
        if (listing == null) return this.NotFoundEnvelope("listing_not_found");
        return this.OkEnvelope("admin.listing.get", listing);
    }

    /// <summary>
    /// POST /api/admin/listings/{id}/approve
    /// نشر إعلان (تغيير الحالة إلى Published).
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var listing = await _repo.GetByIdAsync(id, ct);
        if (listing == null) return this.NotFoundEnvelope("listing_not_found");

        var op = Entry.Create("admin.listing.approve")
            .Describe($"Admin approves listing #{id}")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"Listing:{id}", 1, ("role", "listing"))
            .Tag("listing_id", id.ToString())
            .Tag("action", "approve")
            .Execute(async ctx =>
            {
                listing.Status = ListingStatus.Published;
                listing.PublishedAt ??= DateTime.UtcNow;
                listing.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(listing, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("listing_approve_failed", result.ErrorMessage);

        return this.OkEnvelope("admin.listing.approve", new { listing.Id, listing.Status });
    }

    /// <summary>
    /// POST /api/admin/listings/{id}/reject
    /// رفض إعلان (تغيير الحالة إلى Rejected).
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectListingRequest? req, CancellationToken ct)
    {
        var listing = await _repo.GetByIdAsync(id, ct);
        if (listing == null) return this.NotFoundEnvelope("listing_not_found");

        var op = Entry.Create("admin.listing.reject")
            .Describe($"Admin rejects listing #{id}")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"Listing:{id}", 1, ("role", "listing"))
            .Tag("listing_id", id.ToString())
            .Tag("action", "reject")
            .Tag("reason", req?.Reason ?? "")
            .Execute(async ctx =>
            {
                listing.Status = ListingStatus.Rejected;
                listing.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(listing, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("listing_reject_failed", result.ErrorMessage);

        return this.OkEnvelope("admin.listing.reject", new { listing.Id, listing.Status });
    }

    /// <summary>
    /// POST /api/admin/listings/{id}/feature
    /// تبديل حالة التمييز (Featured) للإعلان.
    /// </summary>
    [HttpPost("{id:guid}/feature")]
    public async Task<IActionResult> Feature(Guid id, CancellationToken ct)
    {
        var listing = await _repo.GetByIdAsync(id, ct);
        if (listing == null) return this.NotFoundEnvelope("listing_not_found");

        var op = Entry.Create("admin.listing.feature")
            .Describe($"Admin toggles featured for listing #{id} (current: {listing.IsFeatured})")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"Listing:{id}", 1, ("role", "listing"))
            .Tag("listing_id", id.ToString())
            .Tag("action", "feature")
            .Execute(async ctx =>
            {
                listing.IsFeatured = !listing.IsFeatured;
                listing.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(listing, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("listing_feature_failed", result.ErrorMessage);

        return this.OkEnvelope("admin.listing.feature", new { listing.Id, listing.IsFeatured });
    }

    /// <summary>
    /// DELETE /api/admin/listings/{id}
    /// حذف ناعم للإعلان.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var listing = await _repo.GetByIdAsync(id, ct);
        if (listing == null) return this.NotFoundEnvelope("listing_not_found");

        var op = Entry.Create("admin.listing.delete")
            .Describe($"Admin soft-deletes listing #{id}")
            .From($"Admin:system", 1, ("role", "admin"))
            .To($"System:archive", 1, ("role", "archive"))
            .Tag("listing_id", id.ToString())
            .Tag("action", "delete")
            .Execute(async ctx =>
            {
                await _repo.SoftDeleteAsync(id, ctx.CancellationToken);
            })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("listing_delete_failed", result.ErrorMessage);

        return this.NoContentEnvelope("admin.listing.delete");
    }

    public record RejectListingRequest(string? Reason);
}
