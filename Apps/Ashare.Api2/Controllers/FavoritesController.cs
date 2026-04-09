using ACommerce.Favorites.Operations;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Api2.Controllers;

[ApiController]
[Route("api/favorites")]
public class FavoritesController : ControllerBase
{
    private readonly FavoriteService _service;

    public FavoritesController(FavoriteService service) => _service = service;

    public record AddFavoriteRequest(Guid UserId, string EntityType, Guid EntityId, string? Note, string? ListName);

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddFavoriteRequest req, CancellationToken ct)
    {
        var id = await _service.AddAsync(
            req.UserId, req.EntityType, req.EntityId,
            req.Note, req.ListName ?? "default", ct);

        if (id == null) return this.BadRequestEnvelope("favorite_add_failed");
        return this.OkEnvelope("favorite.add", new { favoriteId = id });
    }

    public record RemoveFavoriteRequest(Guid UserId, string EntityType, Guid EntityId, string? ListName);

    [HttpDelete]
    public async Task<IActionResult> Remove([FromBody] RemoveFavoriteRequest req, CancellationToken ct)
    {
        var count = await _service.RemoveAsync(
            req.UserId, req.EntityType, req.EntityId,
            req.ListName ?? "default", ct);

        return this.OkEnvelope("favorite.remove", new { removedCount = count });
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> ListByUser(
        Guid userId,
        [FromQuery] string? entityType = null,
        [FromQuery] string listName = "default",
        CancellationToken ct = default)
    {
        var list = await _service.GetUserFavoritesAsync(userId, entityType, listName, ct);
        return this.OkEnvelope("favorite.list", list.ToList());
    }

    [HttpGet("check")]
    public async Task<IActionResult> Check(
        [FromQuery] Guid userId,
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        [FromQuery] string listName = "default",
        CancellationToken ct = default)
    {
        var isFav = await _service.IsFavoriteAsync(userId, entityType, entityId, listName, ct);
        return this.OkEnvelope("favorite.check", new { isFavorite = isFav });
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        CancellationToken ct = default)
    {
        var count = await _service.CountAsync(entityType, entityId, ct);
        return this.OkEnvelope("favorite.count", new { count });
    }
}
