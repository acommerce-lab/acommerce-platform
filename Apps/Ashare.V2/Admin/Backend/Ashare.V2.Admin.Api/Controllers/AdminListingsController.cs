using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.V2.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.V2.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/listings")]
[Authorize(Policy = "AdminOnly")]
public class AdminListingsController : ControllerBase
{
    private readonly IBaseAsyncRepository<ProductListing> _listings;
    private readonly IBaseAsyncRepository<Product>        _products;
    private readonly OpEngine _engine;

    public AdminListingsController(IRepositoryFactory factory, OpEngine engine)
    {
        _listings = factory.CreateRepository<ProductListing>();
        _products = factory.CreateRepository<Product>();
        _engine   = engine;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _listings.GetPagedAsync(
            pageNumber: page, pageSize: pageSize,
            predicate: l => status == null || l.Status == status.Value,
            orderBy: l => l.CreatedAt, ascending: false);

        var rows = result.Items.Select(l => new
        {
            id         = l.Id,
            productId  = l.ProductId,
            ownerId    = l.OwnerId,
            price      = l.Price,
            currency   = l.Currency,
            city       = l.City,
            district   = l.District,
            status     = l.Status,
            isFeatured = l.IsFeatured,
            viewCount  = l.ViewCount,
            createdAt  = l.CreatedAt
        });
        return this.OkEnvelope("admin.listing.list", rows);
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var l = await _listings.GetByIdAsync(id, ct);
        if (l == null) return this.NotFoundEnvelope("listing_not_found");

        var op = Entry.Create("admin.listing.publish")
            .Describe($"Admin publishes Listing:{id}")
            .From("Admin:system", 1, ("role", "admin"))
            .To($"Listing:{id}", 1, ("role", "listing"))
            .Tag("listing_id", id.ToString())
            .Execute(async ctx =>
            {
                l.Status    = 1;
                l.UpdatedAt = DateTime.UtcNow;
                await _listings.UpdateAsync(l, ctx.CancellationToken);
            })
            .Build();

        var res = await _engine.ExecuteAsync(op, ct);
        if (!res.Success) return this.BadRequestEnvelope("publish_failed", res.ErrorMessage);
        return this.OkEnvelope("admin.listing.publish", new { l.Id, l.Status });
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        var l = await _listings.GetByIdAsync(id, ct);
        if (l == null) return this.NotFoundEnvelope("listing_not_found");

        var op = Entry.Create("admin.listing.reject")
            .Describe($"Admin rejects Listing:{id}")
            .From("Admin:system", 1, ("role", "admin"))
            .To($"Listing:{id}", 1, ("role", "listing"))
            .Tag("listing_id", id.ToString())
            .Execute(async ctx =>
            {
                l.Status    = 3;
                l.UpdatedAt = DateTime.UtcNow;
                await _listings.UpdateAsync(l, ctx.CancellationToken);
            })
            .Build();

        var res = await _engine.ExecuteAsync(op, ct);
        if (!res.Success) return this.BadRequestEnvelope("reject_failed", res.ErrorMessage);
        return this.OkEnvelope("admin.listing.reject", new { l.Id, l.Status });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var l = await _listings.GetByIdAsync(id, ct);
        if (l == null) return this.NotFoundEnvelope("listing_not_found");

        var op = Entry.Create("admin.listing.delete")
            .Describe($"Admin deletes Listing:{id}")
            .From("Admin:system", 1, ("role", "admin"))
            .To($"Listing:{id}", 1, ("role", "listing"))
            .Tag("listing_id", id.ToString())
            .Execute(async ctx =>
            {
                l.IsDeleted = true;
                l.UpdatedAt = DateTime.UtcNow;
                await _listings.UpdateAsync(l, ctx.CancellationToken);
            })
            .Build();

        var res = await _engine.ExecuteAsync(op, ct);
        if (!res.Success) return this.BadRequestEnvelope("delete_failed", res.ErrorMessage);
        return this.OkEnvelope("admin.listing.delete", new { id });
    }
}
