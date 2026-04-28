using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Ejar.Api.Data;
using Ejar.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Controllers;

/// <summary>
/// نقاط نهاية عامة — لا تتطلب مصادقة:
/// قائمة الإعلانات مع الفلاتر، التفاصيل، التصنيفات، المدن، المميزات، الإصدار.
/// كل البيانات تُقرأ من قاعدة البيانات مباشرة عبر EjarDbContext.
/// </summary>
[ApiController]
public class HomeController : ControllerBase
{
    private readonly EjarDbContext _db;
    public HomeController(EjarDbContext db) => _db = db;

    // ── GET /listings ────────────────────────────────────────────────────
    [HttpGet("/listings")]
    public async Task<IActionResult> Listings(
        [FromQuery] string? city,
        [FromQuery] string? district,
        [FromQuery] string? propertyType,
        [FromQuery] string? timeUnit,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? q,
        [FromQuery] double? lat,
        [FromQuery] double? lng,
        [FromQuery] double? radius,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.Listings.AsNoTracking().Where(l => l.Status == 1);

        if (!string.IsNullOrWhiteSpace(city))         query = query.Where(l => l.City.Contains(city));
        if (!string.IsNullOrWhiteSpace(district))     query = query.Where(l => l.District.Contains(district));
        if (!string.IsNullOrWhiteSpace(propertyType)) query = query.Where(l => l.PropertyType == propertyType);
        if (!string.IsNullOrWhiteSpace(timeUnit))     query = query.Where(l => l.TimeUnit == timeUnit);
        if (minPrice.HasValue)                        query = query.Where(l => l.Price >= minPrice.Value);
        if (maxPrice.HasValue)                        query = query.Where(l => l.Price <= maxPrice.Value);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(l => l.Title.Contains(q) || l.Description.Contains(q) ||
                                     l.City.Contains(q) || l.District.Contains(q));

        var total = await query.CountAsync();

        query = sort switch
        {
            "price_asc"  => query.OrderBy(l => l.Price),
            "price_desc" => query.OrderByDescending(l => l.Price),
            "views"      => query.OrderByDescending(l => l.ViewsCount),
            _            => query.OrderByDescending(l => l.ViewsCount)
        };

        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // Geo filter (post-query — SQLite doesn't support spatial)
        if (lat.HasValue && lng.HasValue && radius.HasValue)
            items = items.Where(l => HaversineKm(lat.Value, lng.Value, l.Lat, l.Lng) <= radius.Value).ToList();

        var userId = GetCurrentUserId();
        var favIds = userId.HasValue
            ? await _db.Favorites.AsNoTracking().Where(f => f.UserId == userId.Value).Select(f => f.ListingId).ToListAsync()
            : new List<Guid>();

        return this.OkEnvelope("listing.list", new
        {
            total, page, pageSize,
            items = items.Select(l => MapSummary(l, favIds))
        });
    }

    // ── GET /listings/{id} ───────────────────────────────────────────────
    [HttpGet("/listings/{id:guid}")]
    public async Task<IActionResult> ListingDetails(Guid id)
    {
        var l = await _db.Listings.FindAsync(id);
        if (l is null) return this.NotFoundEnvelope("listing_not_found");

        // increment views
        l.ViewsCount++;
        await _db.SaveChangesAsync();

        var owner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == l.OwnerId);
        var userId = GetCurrentUserId();
        var isFav = userId.HasValue &&
            await _db.Favorites.AnyAsync(f => f.UserId == userId.Value && f.ListingId == id);

        return this.OkEnvelope("listing.details", new
        {
            id = l.Id, title = l.Title, description = l.Description,
            price = l.Price, timeUnit = l.TimeUnit,
            timeUnitLabel = TimeUnitLabel(l.TimeUnit),
            propertyType = l.PropertyType,
            propertyTypeLabel = CategoryLabel(l.PropertyType),
            city = l.City, district = l.District, lat = l.Lat, lng = l.Lng,
            amenities = SplitCsv(l.AmenitiesCsv).Select(a => new {
                key = a, label = EjarSeed.AmenityLabels.GetValueOrDefault(a, a)
            }),
            ownerId = l.OwnerId,
            owner = new {
                name = owner?.FullName ?? "المؤجر",
                memberSince = owner?.MemberSince.Year.ToString() ?? "2024"
            },
            bedroomCount = l.BedroomCount, bathroomCount = l.BathroomCount,
            areaSqm = l.AreaSqm, isVerified = l.IsVerified,
            viewsCount = l.ViewsCount, status = l.Status,
            isFavorite = isFav,
            images = SplitCsv(l.ImagesCsv)
        });
    }

    // ── GET /home/view ───────────────────────────────────────────────────
    [HttpGet("/home/view")]
    public async Task<IActionResult> HomeView([FromQuery] string? city = null)
    {
        var query = _db.Listings.AsNoTracking().Where(l => l.Status == 1);
        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(l => l.City == city);

        var items = await query.ToListAsync();

        var userId = GetCurrentUserId();
        var favIds = userId.HasValue
            ? await _db.Favorites.AsNoTracking().Where(f => f.UserId == userId.Value).Select(f => f.ListingId).ToListAsync()
            : new List<Guid>();

        var featured = items.Where(l => l.IsVerified).Select(l => MapSummary(l, favIds)).ToList();
        var @new     = items.Where(l => !l.IsVerified).Take(6).Select(l => MapSummary(l, favIds)).ToList();

        return this.OkEnvelope("home.view", new
        {
            categories = EjarSeed.Categories.Select(c => new {
                id = c.Id, label = c.Label, icon = MapEmojiToIcon(c.Emoji)
            }),
            featured, @new, city
        });
    }

    // ── GET /home/explore ────────────────────────────────────────────────
    [HttpGet("/home/explore")]
    public async Task<IActionResult> Explore(
        [FromQuery] string? category,
        [FromQuery] string? city,
        [FromQuery] decimal? priceMin,
        [FromQuery] decimal? priceMax,
        [FromQuery] int? capacity,
        [FromQuery] int? minRating,
        [FromQuery] string? q = null,
        [FromQuery] string? sort = "newest")
    {
        var query = _db.Listings.AsNoTracking().Where(l => l.Status == 1);

        if (!string.IsNullOrWhiteSpace(city))     query = query.Where(l => l.City == city);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(l => l.PropertyType == category);
        if (priceMin.HasValue)                    query = query.Where(l => l.Price >= priceMin.Value);
        if (priceMax.HasValue)                    query = query.Where(l => l.Price <= priceMax.Value);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(l => l.Title.Contains(q) || l.Description.Contains(q) || l.District.Contains(q));

        // capacity filter — post-query for SQLite compatibility
        var results = await query.ToListAsync();
        if (capacity.HasValue && capacity.Value > 0)
        {
            var cap = capacity.Value;
            results = results.Where(l => cap switch
            {
                1 => l.BedroomCount <= 1,
                2 => l.BedroomCount == 2,
                3 => l.BedroomCount == 3,
                4 => l.BedroomCount >= 4,
                _ => true
            }).ToList();
        }

        results = sort switch
        {
            "price_low"  => results.OrderBy(l => l.Price).ToList(),
            "price_high" => results.OrderByDescending(l => l.Price).ToList(),
            "rating"     => results.OrderByDescending(l => l.IsVerified).ToList(),
            _            => results
        };

        var userId = GetCurrentUserId();
        var favIds = userId.HasValue
            ? await _db.Favorites.AsNoTracking().Where(f => f.UserId == userId.Value).Select(f => f.ListingId).ToListAsync()
            : new List<Guid>();

        return this.OkEnvelope("catalog.list", results.Select(l => MapSummary(l, favIds)).ToList());
    }

    // ── GET /home/search/suggestions ─────────────────────────────────────
    [HttpGet("/home/search/suggestions")]
    public IActionResult SearchSuggestions() =>
        this.OkEnvelope("search.suggestions", new
        {
            popular = EjarSeed.PopularSearches,
            quickFilters = EjarSeed.QuickFilters.Select(q => new {
                id = q.Id, label = q.Label, icon = q.Icon
            })
        });

    // ── GET /categories ──────────────────────────────────────────────────
    [HttpGet("/categories")]
    public IActionResult Categories() =>
        this.OkEnvelope("categories.list",
            EjarSeed.Categories.Select(c => new {
                id = c.Id, label = c.Label, emoji = c.Emoji,
                kind = c.Kind, timeUnits = c.TimeUnits
            }));

    // ── GET /cities ──────────────────────────────────────────────────────
    [HttpGet("/cities")]
    public IActionResult Cities() =>
        this.OkEnvelope("cities.list", EjarSeed.Cities);

    // ── GET /amenities ───────────────────────────────────────────────────
    [HttpGet("/amenities")]
    public IActionResult Amenities() =>
        this.OkEnvelope("amenities.list",
            EjarSeed.AmenityLabels.Select(kv => new { key = kv.Key, label = kv.Value }));

    // ── GET /version/check ───────────────────────────────────────────────
    [HttpGet("/version/check")]
    public IActionResult VersionCheck()
    {
        var v = EjarSeed.Version;
        return this.OkEnvelope("app.version.check", new {
            current = v.Current, latest = v.Latest,
            isBlocked = v.IsBlocked, storeUrl = v.StoreUrl,
            supportEmail = v.SupportEmail
        });
    }

    // ── GET /legal ────────────────────────────────────────────────────────
    [HttpGet("/legal")]
    public IActionResult LegalAll() =>
        this.OkEnvelope("legal.list",
            EjarSeed.Legal.Select(d => new { key = d.Key, title = d.Title }));

    [HttpGet("/legal/{key}")]
    public IActionResult Legal(string key)
    {
        var doc = EjarSeed.Legal.FirstOrDefault(l => l.Key == key);
        if (doc is null) return this.NotFoundEnvelope("legal_not_found");
        return this.OkEnvelope("legal.fetch", new { key = doc.Key, title = doc.Title, body = doc.Body });
    }

    // ── helpers ──────────────────────────────────────────────────────────
    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst("user_id")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private static object MapSummary(ListingEntity l, List<Guid> favIds) => new
    {
        id = l.Id, title = l.Title,
        price = l.Price, timeUnit = l.TimeUnit,
        timeUnitLabel = TimeUnitLabel(l.TimeUnit),
        propertyType = l.PropertyType,
        propertyTypeLabel = CategoryLabel(l.PropertyType),
        city = l.City, district = l.District, lat = l.Lat, lng = l.Lng,
        bedroomCount = l.BedroomCount, areaSqm = l.AreaSqm,
        isVerified = l.IsVerified, viewsCount = l.ViewsCount,
        isFavorite = favIds.Contains(l.Id),
        amenities = SplitCsv(l.AmenitiesCsv).Take(4),
        firstImage = SplitCsv(l.ImagesCsv).FirstOrDefault()
    };

    private static string TimeUnitLabel(string u) => u switch
    {
        "monthly" => "شهرياً",
        "yearly"  => "سنوياً",
        "daily"   => "يومياً",
        "hourly"  => "بالساعة",
        _         => u
    };

    private static string CategoryLabel(string t) =>
        EjarSeed.Categories.FirstOrDefault(c => c.Id == t)?.Label ?? t;

    private static string MapEmojiToIcon(string emoji) => emoji switch
    {
        "🏠" => "home",
        "🏢" => "building",
        "🏬" => "shopping-bag",
        "🏗️" => "map",
        "🏪" => "store",
        "🏨" => "hotel",
        _    => "tag"
    };

    private static IReadOnlyList<string> SplitCsv(string? s) =>
        string.IsNullOrEmpty(s)
            ? Array.Empty<string>()
            : s.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries);

    private static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
