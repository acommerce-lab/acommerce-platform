using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;
using Order.Api.Entities;

namespace Order.Api.Controllers;

[ApiController]
[Route("api/favorites")]
public class FavoritesController : ControllerBase
{
    private readonly IBaseAsyncRepository<Favorite> _repo;
    private readonly IBaseAsyncRepository<Offer> _offers;
    private readonly IBaseAsyncRepository<Vendor> _vendors;
    private readonly OpEngine _engine;

    public FavoritesController(IRepositoryFactory factory, OpEngine engine)
    {
        _repo = factory.CreateRepository<Favorite>();
        _offers = factory.CreateRepository<Offer>();
        _vendors = factory.CreateRepository<Vendor>();
        _engine = engine;
    }

    public record ToggleRequest(Guid UserId, Guid OfferId);

    [HttpPost("toggle")]
    public async Task<IActionResult> Toggle([FromBody] ToggleRequest req, CancellationToken ct)
    {
        var existing = await _repo.GetAllWithPredicateAsync(
            f => f.UserId == req.UserId && f.OfferId == req.OfferId);

        if (existing.Count > 0)
        {
            var fav = existing.First();
            var removeOp = Entry.Create("favorite.remove")
                .Describe($"User:{req.UserId} unfavorites Offer:{req.OfferId}")
                .From($"User:{req.UserId}", 1, ("role", "customer"))
                .To($"Offer:{req.OfferId}", 1, ("role", "offer"))
                .Tag("offer_id", req.OfferId.ToString())
                .Execute(async ctx =>
                {
                    await _repo.DeleteAsync(fav, ctx.CancellationToken);
                })
                .Build();

            var result = await _engine.ExecuteAsync(removeOp, ct);
            if (!result.Success) return this.BadRequestEnvelope("favorite_remove_failed", result.ErrorMessage);
            return this.OkEnvelope("favorite.remove", new { isFavorite = false });
        }

        var newFav = new Favorite
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UserId = req.UserId,
            OfferId = req.OfferId
        };

        var addOp = Entry.Create("favorite.add")
            .Describe($"User:{req.UserId} favorites Offer:{req.OfferId}")
            .From($"User:{req.UserId}", 1, ("role", "customer"))
            .To($"Offer:{req.OfferId}", 1, ("role", "offer"))
            .Tag("offer_id", req.OfferId.ToString())
            .Execute(async ctx =>
            {
                await _repo.AddAsync(newFav, ctx.CancellationToken);
                ctx.Set("favoriteId", newFav.Id);
            })
            .Build();

        var addResult = await _engine.ExecuteAsync(addOp, ct);
        if (!addResult.Success) return this.BadRequestEnvelope("favorite_add_failed", addResult.ErrorMessage);
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
