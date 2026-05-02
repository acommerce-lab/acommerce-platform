using ACommerce.Kits.Listings.Backend;
using ACommerce.Kits.Listings.Domain;
using ACommerce.Kits.Listings.Operations;
using Ejar.Api.Data;
using Ejar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Stores;

/// <summary>
/// مخزن إعلانات إيجار. يلائم
/// <see cref="IListingStore"/> عبر تحويل
/// <see cref="ListingEntity"/> ↔ <see cref="IListing"/>.
/// كلّ الـ writes tracker-only (F6) — السلاسل تُحفَظ بـ <c>SaveAtEnd</c>
/// على القيد في <see cref="ListingsController"/>.
/// </summary>
public sealed class EjarListingStore : IListingStore
{
    private readonly EjarDbContext _db;
    public EjarListingStore(EjarDbContext db) => _db = db;

    // ── reads ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<IListing>> SearchAsync(ListingFilter f, CancellationToken ct)
    {
        var q = ApplyFilter(_db.Listings.AsNoTracking().Where(l => l.Status == 1), f);
        q = ApplySort(q, f.Sort);
        var rows = await q.Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct);
        return rows.Select(ToView).ToList();
    }

    public async Task<int> CountAsync(ListingFilter f, CancellationToken ct) =>
        await ApplyFilter(_db.Listings.AsNoTracking().Where(l => l.Status == 1), f).CountAsync(ct);

    public async Task<IListing?> GetAsync(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var gid)) return null;
        var l = await _db.Listings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == gid, ct);
        return l is null ? null : ToView(l);
    }

    public async Task<IReadOnlyList<IListing>> ListByOwnerAsync(string ownerId, CancellationToken ct)
    {
        if (!Guid.TryParse(ownerId, out var oid)) return Array.Empty<IListing>();
        var rows = await _db.Listings.AsNoTracking()
            .Where(l => l.OwnerId == oid)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(ToView).ToList();
    }

    // ── writes ───────────────────────────────────────────────────────────

    public Task AddNoSaveAsync(IListing listing, CancellationToken ct)
    {
        if (!Guid.TryParse(listing.OwnerId, out var oid))
            throw new InvalidOperationException("invalid_owner_id");
        var rowId = Guid.TryParse(listing.Id, out var lid) ? lid : Guid.NewGuid();
        var entity = new ListingEntity
        {
            Id            = rowId,
            CreatedAt     = listing.CreatedAt,
            Title         = listing.Title,
            Description   = listing.Description,
            Price         = listing.Price,
            TimeUnit      = listing.TimeUnit,
            PropertyType  = listing.PropertyType,
            City          = listing.City,
            District      = listing.District,
            Lat           = listing.Lat,
            Lng           = listing.Lng,
            OwnerId       = oid,
            BedroomCount  = listing.BedroomCount,
            BathroomCount = listing.BathroomCount,
            AreaSqm       = listing.AreaSqm,
            Status        = listing.Status,
            ImagesCsv     = string.Join("|", listing.Images),
            ThumbnailUrl  = listing.ThumbnailUrl,
            AmenitiesCsv  = string.Join(",", listing.Amenities),
        };
        _db.Listings.Add(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> UpdateNoSaveAsync(string id, ListingUpdate p, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var gid)) return false;
        var l = await _db.Listings.FirstOrDefaultAsync(x => x.Id == gid && !x.IsDeleted, ct);
        if (l is null) return false;

        if (p.Title         is { Length: > 0 }) l.Title         = p.Title;
        if (p.Description   is not null)        l.Description   = p.Description;
        if (p.Price.HasValue)                   l.Price         = p.Price.Value;
        if (p.TimeUnit      is { Length: > 0 }) l.TimeUnit      = p.TimeUnit;
        if (p.PropertyType  is { Length: > 0 }) l.PropertyType  = p.PropertyType;
        if (p.City          is { Length: > 0 }) l.City          = p.City;
        if (p.District      is not null)        l.District      = p.District;
        if (p.Lat.HasValue)                     l.Lat           = p.Lat.Value;
        if (p.Lng.HasValue)                     l.Lng           = p.Lng.Value;
        if (p.BedroomCount.HasValue)            l.BedroomCount  = p.BedroomCount.Value;
        if (p.BathroomCount.HasValue)           l.BathroomCount = p.BathroomCount.Value;
        if (p.AreaSqm.HasValue)                 l.AreaSqm       = p.AreaSqm.Value;
        if (p.Amenities is not null)            l.AmenitiesCsv  = string.Join(",", p.Amenities);
        if (p.Images    is not null)            l.ImagesCsv     = string.Join("|", p.Images);
        if (p.Thumbnail is not null)
            l.ThumbnailUrl = string.IsNullOrEmpty(p.Thumbnail) ? null : p.Thumbnail;
        l.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public async Task<int?> ToggleStatusNoSaveAsync(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var gid)) return null;
        var l = await _db.Listings.FirstOrDefaultAsync(x => x.Id == gid && !x.IsDeleted, ct);
        if (l is null) return null;
        l.Status = l.Status == 1 ? 2 : 1;
        l.UpdatedAt = DateTime.UtcNow;
        return l.Status;
    }

    public async Task<bool> DeleteNoSaveAsync(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var gid)) return false;
        var l = await _db.Listings.FirstOrDefaultAsync(x => x.Id == gid && !x.IsDeleted, ct);
        if (l is null) return false;
        l.IsDeleted = true;
        l.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public async Task<bool> IsOwnerAsync(string id, string ownerId, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var gid))           return false;
        if (!Guid.TryParse(ownerId, out var oid))      return false;
        return await _db.Listings.AsNoTracking()
            .AnyAsync(x => x.Id == gid && x.OwnerId == oid && !x.IsDeleted, ct);
    }

    public async Task IncrementViewCountNoSaveAsync(string id, CancellationToken ct)
    {
        // ExecuteUpdateAsync ذرّيّ بدون tracking — أفضل من جلب الكيان وزيادته
        // (فيه race بين قارئَين). يعمل بدون SaveChanges.
        if (!Guid.TryParse(id, out var gid)) return;
        await _db.Listings.Where(l => l.Id == gid)
            .ExecuteUpdateAsync(u => u.SetProperty(l => l.ViewsCount, l => l.ViewsCount + 1), ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static IQueryable<ListingEntity> ApplyFilter(IQueryable<ListingEntity> q, ListingFilter f)
    {
        if (!string.IsNullOrWhiteSpace(f.City))         q = q.Where(l => l.City.Contains(f.City));
        if (!string.IsNullOrWhiteSpace(f.District))     q = q.Where(l => l.District.Contains(f.District));
        if (!string.IsNullOrWhiteSpace(f.PropertyType)) q = q.Where(l => l.PropertyType == f.PropertyType);
        if (!string.IsNullOrWhiteSpace(f.TimeUnit))     q = q.Where(l => l.TimeUnit == f.TimeUnit);
        if (f.PriceMin.HasValue)                        q = q.Where(l => l.Price >= f.PriceMin.Value);
        if (f.PriceMax.HasValue)                        q = q.Where(l => l.Price <= f.PriceMax.Value);
        if (f.MinBedrooms > 0)                          q = q.Where(l => l.BedroomCount >= f.MinBedrooms);
        if (f.MinAreaSqm > 0)                           q = q.Where(l => l.AreaSqm >= f.MinAreaSqm);
        if (f.OnlyVerified)                             q = q.Where(l => l.IsVerified);
        if (!string.IsNullOrWhiteSpace(f.Search))
            q = q.Where(l => l.Title.Contains(f.Search) || l.Description.Contains(f.Search) ||
                             l.City.Contains(f.Search)  || l.District.Contains(f.Search));
        return q;
    }

    private static IQueryable<ListingEntity> ApplySort(IQueryable<ListingEntity> q, string? sort) => sort switch
    {
        "newest"     => q.OrderByDescending(l => l.CreatedAt),
        "price_asc"  => q.OrderBy(l => l.Price),
        "price_desc" => q.OrderByDescending(l => l.Price),
        _            => q.OrderByDescending(l => l.ViewsCount),
    };

    private static IListing ToView(ListingEntity e) => new InMemoryListing(
        Id:            e.Id.ToString(),
        OwnerId:       e.OwnerId.ToString(),
        Title:         e.Title,
        Description:   e.Description,
        Price:         e.Price,
        TimeUnit:      e.TimeUnit,
        PropertyType:  e.PropertyType,
        City:          e.City,
        District:      e.District,
        Lat:           e.Lat,
        Lng:           e.Lng,
        BedroomCount:  e.BedroomCount,
        BathroomCount: e.BathroomCount,
        AreaSqm:       e.AreaSqm,
        Status:        e.Status,
        ViewsCount:    e.ViewsCount,
        IsVerified:    e.IsVerified,
        ThumbnailUrl:  e.ThumbnailUrl,
        Images:        string.IsNullOrEmpty(e.ImagesCsv)
                         ? Array.Empty<string>()
                         : e.ImagesCsv.Split('|', StringSplitOptions.RemoveEmptyEntries),
        Amenities:     string.IsNullOrEmpty(e.AmenitiesCsv)
                         ? Array.Empty<string>()
                         : e.AmenitiesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries),
        CreatedAt:     e.CreatedAt,
        UpdatedAt:     e.UpdatedAt);
}
