using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.V2.Api.Entities;

namespace Order.V2.Vendor.Api.Controllers;

[ApiController]
[Route("api/vendor/offers")]
[Authorize(Policy = "VendorOnly")]
public class VendorOffersController : ControllerBase
{
    private readonly IBaseAsyncRepository<Offer>    _offers;
    private readonly IBaseAsyncRepository<Category> _cats;
    private readonly OpEngine _engine;

    public VendorOffersController(IRepositoryFactory repo, OpEngine engine)
    {
        _offers = repo.CreateRepository<Offer>();
        _cats   = repo.CreateRepository<Category>();
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var vendorId = Guid.Parse(User.FindFirst("vendor_id")!.Value);

        var all = (await _offers.ListAllAsync(ct))
            .Where(o => o.VendorId == vendorId && !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.Id, o.Title, o.Description, o.Emoji,
                o.Price, o.OriginalPrice, o.Currency,
                o.IsActive, o.IsFeatured, o.DiscountPercent,
                o.QuantityAvailable, o.CategoryId, o.CreatedAt
            }).ToList();

        return this.OkEnvelope("vendor.offers.list", all);
    }

    public record CreateOfferBody(string Title, string Description, decimal Price,
                                   decimal? OriginalPrice, Guid CategoryId,
                                   string? Emoji, int? QuantityAvailable);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOfferBody req, CancellationToken ct)
    {
        var vendorId = Guid.Parse(User.FindFirst("vendor_id")!.Value);
        var id = Guid.NewGuid();

        var offer = new Offer
        {
            Id = id, CreatedAt = DateTime.UtcNow,
            VendorId = vendorId, CategoryId = req.CategoryId,
            Title = req.Title.Trim(), Description = req.Description.Trim(),
            Price = req.Price, OriginalPrice = req.OriginalPrice,
            Currency = "SAR", Emoji = req.Emoji ?? "🍽️",
            QuantityAvailable = req.QuantityAvailable ?? 100,
            IsActive = true
        };

        var op = Entry.Create("vendor.offer.create")
            .Describe($"Vendor:{vendorId} creates Offer:{id}")
            .From($"Vendor:{vendorId}", 1, ("role", "vendor"))
            .To($"Offer:{id}", 1, ("role", "offer"))
            .Tag("vendor_id", vendorId.ToString())
            .Analyze(new RequiredFieldAnalyzer("title", () => req.Title))
            .Execute(async ctx => await _offers.AddAsync(offer, ctx.CancellationToken))
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("create_failed", result.ErrorMessage);
        return this.OkEnvelope("vendor.offer.create", new { id, offer.Title, offer.Price });
    }

    [HttpPost("{id}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct)
    {
        var vendorId = Guid.Parse(User.FindFirst("vendor_id")!.Value);
        var offer = await _offers.GetByIdAsync(id, ct);
        if (offer is null || offer.VendorId != vendorId) return this.NotFoundEnvelope("offer_not_found");

        var newActive = !offer.IsActive;
        var opType = newActive ? "vendor.offer.activate" : "vendor.offer.deactivate";

        var op = Entry.Create(opType)
            .Describe($"Vendor:{vendorId} toggles Offer:{id} to {newActive}")
            .From($"Vendor:{vendorId}", 1, ("role", "vendor"))
            .To($"Offer:{id}", 1, ("role", "offer"))
            .Execute(async ctx => { offer.IsActive = newActive; await _offers.UpdateAsync(offer, ctx.CancellationToken); })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("toggle_failed", result.ErrorMessage);
        return this.OkEnvelope(opType, new { offerId = id, isActive = newActive });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var vendorId = Guid.Parse(User.FindFirst("vendor_id")!.Value);
        var offer = await _offers.GetByIdAsync(id, ct);
        if (offer is null || offer.VendorId != vendorId) return this.NotFoundEnvelope("offer_not_found");

        var op = Entry.Create("vendor.offer.delete")
            .Describe($"Vendor:{vendorId} deletes Offer:{id}")
            .From($"Vendor:{vendorId}", 1, ("role", "vendor"))
            .To($"Offer:{id}", 1, ("role", "offer"))
            .Execute(async ctx => { offer.IsDeleted = true; await _offers.UpdateAsync(offer, ctx.CancellationToken); })
            .Build();

        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return this.BadRequestEnvelope("delete_failed", result.ErrorMessage);
        return this.OkEnvelope("vendor.offer.delete", new { id, deleted = true });
    }
}
