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

    public OffersController(IRepositoryFactory factory)
    {
        _repo = factory.CreateRepository<Offer>();
        _vendors = factory.CreateRepository<Vendor>();
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
        await _repo.AddAsync(offer, ct);
        return this.OkEnvelope("offer.create", new { offer.Id, offer.Title });
    }

    public record UpdateOfferRequest(string? Title, string? Description, decimal? Price, decimal? OriginalPrice, string? Emoji, bool? IsActive);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOfferRequest req, CancellationToken ct)
    {
        var offer = await _repo.GetByIdAsync(id, ct);
        if (offer == null) return this.NotFoundEnvelope("offer_not_found");
        if (req.Title != null) offer.Title = req.Title;
        if (req.Description != null) offer.Description = req.Description;
        if (req.Price.HasValue) offer.Price = req.Price.Value;
        if (req.OriginalPrice.HasValue) offer.OriginalPrice = req.OriginalPrice.Value;
        if (req.Emoji != null) offer.Emoji = req.Emoji;
        if (req.IsActive.HasValue) offer.IsActive = req.IsActive.Value;
        offer.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(offer, ct);
        return this.OkEnvelope("offer.update", new { offer.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var offer = await _repo.GetByIdAsync(id, ct);
        if (offer == null) return this.NotFoundEnvelope("offer_not_found");
        offer.IsActive = false;
        offer.IsDeleted = true;
        offer.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(offer, ct);
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
