using ACommerce.Favorites.Backend;
using ACommerce.Favorites.Operations.Entities;
using Ejar.Api.Data;
using Ejar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Stores;

/// <summary>
/// مخزن مفضّلات إيجار. يَلائم <see cref="IFavoritesStore"/>:
/// <list type="bullet">
///   <item><c>ListMineAsync</c>: join على Listings للحصول على title/price/thumbnail.</item>
///   <item><c>ToggleNoSaveAsync</c>: tracker-only — Add/Remove على ChangeTracker.</item>
/// </list>
/// </summary>
public sealed class EjarFavoritesStore : IFavoritesStore
{
    private readonly EjarDbContext _db;
    public EjarFavoritesStore(EjarDbContext db) => _db = db;

    public async Task<IReadOnlyList<object>> ListMineAsync(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var uid)) return Array.Empty<object>();

        var ids = await _db.Favorites.AsNoTracking()
            .Where(f => f.UserId == uid && f.EntityType == nameof(ListingEntity))
            .Select(f => f.EntityId).ToListAsync(ct);

        if (ids.Count == 0) return Array.Empty<object>();

        var listings = await _db.Listings.AsNoTracking()
            .Where(l => ids.Contains(l.Id) && !l.IsDeleted)
            .ToListAsync(ct);

        return listings.Select(l => (object)new
        {
            id           = l.Id,
            title        = l.Title,
            price        = l.Price,
            timeUnit     = l.TimeUnit,
            propertyType = l.PropertyType,
            city         = l.City,
            district     = l.District,
            isVerified   = l.IsVerified,
            bedroomCount = l.BedroomCount,
            firstImage   = l.ThumbnailUrl
                        ?? (string.IsNullOrEmpty(l.ImagesCsv)
                                ? null
                                : l.ImagesCsv.Split('|', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
        }).ToList();
    }

    public async Task<FavoriteToggleResult> ToggleNoSaveAsync(
        string userId, string entityType, string entityId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var uid))
            throw new InvalidOperationException("invalid_user_id");
        if (!Guid.TryParse(entityId, out var eid))
            throw new InvalidOperationException("invalid_entity_id");

        var existing = await _db.Favorites
            .FirstOrDefaultAsync(f => f.UserId == uid && f.EntityId == eid && f.EntityType == entityType, ct);

        if (existing is null)
        {
            var fav = new Favorite
            {
                Id         = Guid.NewGuid(),
                CreatedAt  = DateTime.UtcNow,
                UserId     = uid,
                EntityType = entityType,
                EntityId   = eid,
            };
            _db.Favorites.Add(fav);
            return new FavoriteToggleResult(eid.ToString(), IsFavorite: true);
        }
        else
        {
            _db.Favorites.Remove(existing);
            return new FavoriteToggleResult(eid.ToString(), IsFavorite: false);
        }
    }
}
