using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Api.Entities;

namespace Order.V2.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/offers")]
[Authorize(Policy = "AdminOnly")]
public class AdminOffersController : ControllerBase
{
    private readonly IBaseAsyncRepository<Offer> _offers;
    private readonly OpEngine _engine;

    public AdminOffersController(IRepositoryFactory repo, OpEngine engine)
    {
        _offers = repo.CreateRepository<Offer>();
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var all = (await _offers.ListAllAsync(ct))
            .Where(o => !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.Id, o.Title, o.VendorId, o.CategoryId,
                o.Price, o.OriginalPrice, o.Currency,
                o.IsActive, o.IsFeatured, o.DiscountPercent, o.CreatedAt
            }).ToList();
        return this.OkEnvelope("admin.offers.list", all);
    }

    [HttpPost("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var offer = await _offers.GetByIdAsync(id, ct);
        if (offer is null) return this.NotFoundEnvelope("offer_not_found");

        var op = Entry.Create("admin.offer.deactivate")
            .Describe($"Admin deactivates Offer:{id}")
            .From("User:admin", 1, ("role", "admin"))
            .To($"Offer:{id}", 1, ("role", "offer"))
            .Execute(async ctx => { offer.IsActive = false; await _offers.UpdateAsync(offer, ctx.CancellationToken); })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("deactivate_failed", result.ErrorMessage);
        return this.OkEnvelope("admin.offer.deactivate", new { offerId = id, active = false });
    }
}
