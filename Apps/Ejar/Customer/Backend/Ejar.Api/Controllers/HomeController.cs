using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using Ejar.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ejar.Api.Controllers;

/// <summary>
/// نقاط نهاية عامة — لا تتطلب مصادقة:
/// قائمة الإعلانات مع الفلاتر، التفاصيل، التصنيفات، المدن، المميزات، الإصدار.
/// </summary>
[ApiController]
public class HomeController : ControllerBase
{
    // ── GET /listings ────────────────────────────────────────────────────
    [HttpGet("/listings")]
    public IActionResult Listings(
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
        var results = EjarSeed.Listings
            .Where(l => l.Status == 1)
            .Where(l => city         == null || l.City.Contains(city))
            .Where(l => district     == null || l.District.Contains(district))
            .Where(l => propertyType == null || l.PropertyType == propertyType)
            .Where(l => timeUnit     == null || l.TimeUnit == timeUnit)
            .Where(l => minPrice     == null || l.Price >= minPrice)
            .Where(l => maxPrice     == null || l.Price <= maxPrice)
            .Where(l => q            == null ||
                        l.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        l.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        l.City.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        l.District.Contains(q, StringComparison.OrdinalIgnoreCase))
            .AsEnumerable();

        // Map-based filter: within radius km
        if (lat.HasValue && lng.HasValue && radius.HasValue)
            results = results.Where(l => HaversineKm(lat.Value, lng.Value, l.Lat, l.Lng) <= radius.Value);

        results = sort switch
        {
            "price_asc"  => results.OrderBy(l => l.Price),
            "price_desc" => results.OrderByDescending(l => l.Price),
            "views"      => results.OrderByDescending(l => l.ViewsCount),
            _            => results.OrderByDescending(l => l.ViewsCount)
        };

        var list = results.ToList();
        var total = list.Count;
        var paged = list.Skip((page - 1) * pageSize).Take(pageSize);

        return this.OkEnvelope("listing.list", new
        {
            total, page, pageSize,
            items = paged.Select(MapSummary)
        });
    }

    // ── GET /listings/{id} ───────────────────────────────────────────────
    [HttpGet("/listings/{id}")]
    public IActionResult ListingDetails(string id)
    {
        var l = EjarSeed.Listings.FirstOrDefault(x => x.Id == id);
        if (l is null) return this.NotFoundEnvelope("listing_not_found");

        // increment views (in-memory)
        var ix = EjarSeed.Listings.FindIndex(x => x.Id == id);
        if (ix >= 0)
            EjarSeed.Listings[ix] = l with { ViewsCount = l.ViewsCount + 1 };

        var owner = EjarSeed.GetUser(l.OwnerId);
        return this.OkEnvelope("listing.details", new
        {
            id = l.Id, title = l.Title, description = l.Description,
            price = l.Price, timeUnit = l.TimeUnit,
            timeUnitLabel = TimeUnitLabel(l.TimeUnit),
            propertyType = l.PropertyType,
            propertyTypeLabel = CategoryLabel(l.PropertyType),
            city = l.City, district = l.District, lat = l.Lat, lng = l.Lng,
            amenities = l.Amenities.Select(a => new {
                key   = a,
                label = EjarSeed.AmenityLabels.GetValueOrDefault(a, a)
            }),
            ownerId = l.OwnerId,
            owner = new {
                name        = owner?.FullName ?? "المؤجر",
                memberSince = "2024"
            },
            bedroomCount = l.BedroomCount, bathroomCount = l.BathroomCount,
            areaSqm = l.AreaSqm, isVerified = l.IsVerified,
            viewsCount = l.ViewsCount, status = l.Status,
            isFavorite = EjarSeed.FavoriteIds.Contains(l.Id),
            images = l.Images ?? Array.Empty<string>()
        });
    }

    // ── GET /home/view ───────────────────────────────────────────────────
    [HttpGet("/home/view")]
    public IActionResult HomeView([FromQuery] string? city = null)
    {
        IEnumerable<EjarSeed.ListingSeed> q = EjarSeed.Listings.Where(l => l.Status == 1);
        if (!string.IsNullOrWhiteSpace(city))
            q = q.Where(l => string.Equals(l.City, city, StringComparison.Ordinal));

        var items = q.ToList();
        var featured = items.Where(l => l.IsVerified).Select(MapSummary).ToList();
        var @new     = items.Where(l => !l.IsVerified).Take(6).Select(MapSummary).ToList();

        return this.OkEnvelope("home.view", new
        {
            categories = EjarSeed.Categories.Select(c => new {
                id = c.Id, label = c.Label, icon = MapEmojiToIcon(c.Emoji)
            }),
            featured,
            @new,
            city
        });
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
    private static object MapSummary(EjarSeed.ListingSeed l) => new
    {
        id = l.Id, title = l.Title,
        price = l.Price, timeUnit = l.TimeUnit,
        timeUnitLabel = TimeUnitLabel(l.TimeUnit),
        propertyType = l.PropertyType,
        propertyTypeLabel = CategoryLabel(l.PropertyType),
        city = l.City, district = l.District, lat = l.Lat, lng = l.Lng,
        bedroomCount = l.BedroomCount, areaSqm = l.AreaSqm,
        isVerified = l.IsVerified, viewsCount = l.ViewsCount,
        isFavorite = EjarSeed.FavoriteIds.Contains(l.Id),
        amenities = l.Amenities.Take(4),
        firstImage = l.Images?.FirstOrDefault()
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
