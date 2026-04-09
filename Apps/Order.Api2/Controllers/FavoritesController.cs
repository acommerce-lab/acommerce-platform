using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Order.Api2.Entities;

namespace Order.Api2.Controllers;

[ApiController]
[Route("api/favorites")]
public class FavoritesController : ControllerBase
{
    private readonly IBaseAsyncRepository<Favorite> _repo;
    private readonly IBaseAsyncRepository<Offer> _offers;
    private readonly IBaseAsyncRepository<Vendor> _vendors;

    public FavoritesController(IRepositoryFactory factory)
    {
        _repo = factory.CreateRepository<Favorite>();
        _offers = factory.CreateRepository<Offer>();
        _vendors = factory.CreateRepository<Vendor>();
    }

    public record ToggleRequest(Guid UserId, Guid OfferId);

    [HttpPost("toggle")]
    public async Task<IActionResult> Toggle([FromBody] ToggleRequest req, CancellationToken ct)
    {
        var existing = await _repo.GetAllWithPredicateAsync(
            f => f.UserId == req.UserId && f.OfferId == req.OfferId);
        if (existing.Count > 0)
        {
            await _repo.DeleteAsync(existing.First(), ct);
            return this.OkEnvelope("favorite.remove", new { isFavorite = false });
        }
        await _repo.AddAsync(new Favorite
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UserId = req.UserId,
            OfferId = req.OfferId
        }, ct);
        return this.OkEnvelope("favorite.add", new { isFavorite = true });
    }

    [HttpGet("by-user/{userId:guid}")]
    public async Task<IActionResult> ByUser(Guid userId, CancellationToken ct)
    {
        var favs = await _repo.GetAllWithPredicateAsync(f => f.UserId == userId);
        var offerIds = favs.Select(f => f.OfferId).ToHashSet();
        var offers = (await _offers.ListAllAsync(ct)).Where(o => offerIds.Contains(o.Id)).ToList();
        var vendors = (await _vendors.ListAllAsync(ct)).ToDictionary(v => v.Id);
        var result = offers.Select(o => new
        {
            o.Id,
            o.Title,
            o.Description,
            o.Price,
            o.OriginalPrice,
            o.Currency,
            o.Emoji,
            DiscountPercent = o.DiscountPercent,
            Vendor = vendors.TryGetValue(o.VendorId, out var v) ? new
            {
                v.Id,
                v.Name,
                v.LogoEmoji,
                v.City,
                v.District
            } : null
        }).ToList();
        return this.OkEnvelope("favorite.list", result);
    }
}
