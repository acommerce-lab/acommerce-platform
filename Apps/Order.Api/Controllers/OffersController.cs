using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Order.Api.Entities;

namespace Order.Api.Controllers;

[ApiController]
[Route("api/offers")]
public class OffersController : ControllerBase
{
    private readonly IBaseAsyncRepository<Offer> _repo;
    private readonly IBaseAsyncRepository<Vendor> _vendors;
    private readonly OpEngine _engine;

    public OffersController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo = factory.CreateRepository<Offer>();
        _vendors = factory.CreateRepository<Vendor>();
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? vendorId,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var offers = await _repo.GetAllWithPredicateAsync(o =>
            o.IsActive &&
            (categoryId == null || o.CategoryId == categoryId) &&
            (vendorId == null || o.VendorId == vendorId) &&
            (o.StartsAt == null || o.StartsAt <= now) &&
            (o.EndsAt == null || o.EndsAt >= now));

        if (!string.IsNullOrWhiteSpace(search))
        {
            offers = offers
                .Where(o => o.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                         || o.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var vendors = (await _vendors.ListAllAsync(ct)).ToDictionary(v => v.Id);

        var result = offers
            .OrderByDescending(o => o.IsFeatured)
            .ThenByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.Id,
                o.Title,
                o.Description,
                o.Price,
                o.OriginalPrice,
                o.Currency,
                o.Emoji,
                o.CategoryId,
                o.QuantityAvailable,
                o.IsFeatured,
                DiscountPercent = o.DiscountPercent,
                Vendor = vendors.TryGetValue(o.VendorId, out var v) ? new
                {
                    v.Id,
                    v.Name,
                    v.City,
                    v.District,
                    v.LogoEmoji,
                    v.Rating,
                    v.RatingCount,
                    v.OpenHours,
                    v.Latitude,
                    v.Longitude
                } : null
            })
            .ToList();

        return this.OkEnvelope("offer.list", result);
    }

    public record CreateOfferRequest(
        Guid VendorId,
        Guid CategoryId,
        string Title,
        string Description,
        decimal Price,
        decimal? OriginalPrice,
        string? Emoji);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOfferRequest req, CancellationToken ct)
    {
        var vendor = await _vendors.GetByIdAsync(req.VendorId, ct);
        if (vendor == null) return this.NotFoundEnvelope("vendor_not_found");

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            VendorId = req.VendorId,
            CategoryId = req.CategoryId,
            Title = req.Title,
            Description = req.Description ?? "",
            Price = req.Price,
            OriginalPrice = req.OriginalPrice,
            Emoji = req.Emoji ?? "🍽️",
            IsActive = true,
            IsFeatured = false,
        };

        var op = Entry.Create("offer.create")
            .Describe($"Vendor:{req.VendorId} publishes offer '{req.Title}' at {req.Price} SAR")
            .From($"Vendor:{req.VendorId}", req.Price, ("role", "vendor"), ("currency", "SAR"))
            .To($"Catalog:{req.CategoryId}", 1, ("role", "catalog"))
            .Tag("vendor_name", vendor.Name)
            .Tag("offer_title", req.Title)
            .Tag("price", req.Price.ToString("0.##"))
            .Execute(async ctx =>
            {
                await _repo.AddAsync(offer, ctx.CancellationToken);
                ctx.Set("offerId", offer.Id);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, offer, ct);
        if (envelope.Operation.Status != "Success") return BadRequest(envelope);
        return this.OkEnvelope("offer.create", new { offer.Id, offer.Title });
    }

    public record UpdateOfferRequest(string? Title, string? Description, decimal? Price, decimal? OriginalPrice, string? Emoji, bool? IsActive);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOfferRequest req, CancellationToken ct)
    {
        var offer = await _repo.GetByIdAsync(id, ct);
        if (offer == null) return this.NotFoundEnvelope("offer_not_found");

        var op = Entry.Create("offer.update")
            .Describe($"Update offer '{offer.Title}' (Vendor:{offer.VendorId})")
            .From($"Vendor:{offer.VendorId}", 1, ("role", "vendor"))
            .To($"Offer:{offer.Id}", 1, ("role", "offer"))
            .Tag("offer_title", req.Title ?? offer.Title)
            .Execute(async ctx =>
            {
                if (req.Title != null) offer.Title = req.Title;
                if (req.Description != null) offer.Description = req.Description;
                if (req.Price.HasValue) offer.Price = req.Price.Value;
                if (req.OriginalPrice.HasValue) offer.OriginalPrice = req.OriginalPrice.Value;
                if (req.Emoji != null) offer.Emoji = req.Emoji;
                if (req.IsActive.HasValue) offer.IsActive = req.IsActive.Value;
                offer.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(offer, ctx.CancellationToken);
                ctx.Set("offerId", offer.Id);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, offer, ct);
        if (envelope.Operation.Status != "Success") return BadRequest(envelope);
        return this.OkEnvelope("offer.update", new { offer.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var offer = await _repo.GetByIdAsync(id, ct);
        if (offer == null) return this.NotFoundEnvelope("offer_not_found");

        var op = Entry.Create("offer.delete")
            .Describe($"Vendor:{offer.VendorId} deletes offer '{offer.Title}'")
            .From($"Vendor:{offer.VendorId}", 1, ("role", "vendor"))
            .To($"Offer:{offer.Id}", 1, ("role", "offer"))
            .Tag("offer_title", offer.Title)
            .Execute(async ctx =>
            {
                offer.IsActive = false;
                offer.IsDeleted = true;
                offer.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(offer, ctx.CancellationToken);
                ctx.Set("offerId", offer.Id);
            })
            .Build();

        var envelope = await _engine.ExecuteEnvelopeAsync(op, new { offer.Id }, ct);
        if (envelope.Operation.Status != "Success") return BadRequest(envelope);
        return this.OkEnvelope("offer.delete", new { offer.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var o = await _repo.GetByIdAsync(id, ct);
        if (o == null) return this.NotFoundEnvelope("offer_not_found");
        var v = await _vendors.GetByIdAsync(o.VendorId, ct);
        return this.OkEnvelope("offer.get", new
        {
            o.Id,
            o.Title,
            o.Description,
            o.Price,
            o.OriginalPrice,
            o.Currency,
            o.Emoji,
            o.CategoryId,
            o.QuantityAvailable,
            o.IsFeatured,
            DiscountPercent = o.DiscountPercent,
            Vendor = v == null ? null : new
            {
                v.Id,
                v.Name,
                v.Description,
                v.City,
                v.District,
                v.Phone,
                v.LogoEmoji,
                v.CoverEmoji,
                v.Rating,
                v.RatingCount,
                v.OpenHours,
                v.Latitude,
                v.Longitude
            }
        });
    }
}
