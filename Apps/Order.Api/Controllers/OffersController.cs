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
                    v.OpenHours
                } : null
            })
            .ToList();

        return this.OkEnvelope("offer.list", result);
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
