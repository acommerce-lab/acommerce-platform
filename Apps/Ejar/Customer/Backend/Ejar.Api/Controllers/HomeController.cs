using ACommerce.Favorites.Operations.Entities;
using ACommerce.Kits.Discovery.Domain;
using ACommerce.OperationEngine.Wire.Http;
using Ejar.Api.Data;
using Ejar.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Controllers;

/// <summary>
/// نقاط النهاية العامّة (لا تتطلّب مصادقة) — مسارات يتوقّعها التطبيق المشترك
/// (Web/WASM/MAUI). راجع <c>docs/EJAR-API-CONTRACT.md</c> للقائمة الكاملة.
///
/// المسارات هنا ليست مسبوقة بـ <c>/api</c> لتطابق ما تستدعيه واجهات الـ Razor
/// مباشرةً (مثل <c>Api.GetAsync("/home/view")</c>).
/// </summary>
[ApiController]
public sealed class HomeController : ControllerBase
{
    private readonly EjarDbContext _db;
    public HomeController(EjarDbContext db) => _db = db;

    // ── /home/view ─────────────────────────────────────────────────────────
    [HttpGet("/home/view")]
    public async Task<IActionResult> HomeView([FromQuery] string? city, CancellationToken ct)
    {
        var listings   = await _db.Listings.AsNoTracking()
            .Where(l => l.Status == 1 && (string.IsNullOrEmpty(city) || l.City == city))
            .ToListAsync(ct);
        var categories = await _db.DiscoveryCategories.AsNoTracking().ToListAsync(ct);

        return this.OkEnvelope("home.view", new {
            categories = categories.Select(c => new { id = c.Slug, label = c.Label, icon = c.Icon }),
            featured   = listings.Where(l => l.IsVerified).Select(l => MapSummary(l, categories)).ToList(),
            @new       = listings.Where(l => !l.IsVerified).Take(6).Select(l => MapSummary(l, categories)).ToList(),
            city
        });
    }

    // ── /home/explore ──────────────────────────────────────────────────────
    [HttpGet("/home/explore")]
    public async Task<IActionResult> HomeExplore(
        [FromQuery] string? sort, [FromQuery] string? category, [FromQuery] string? q,
        CancellationToken ct)
    {
        var query = _db.Listings.AsNoTracking().Where(l => l.Status == 1);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(l => l.PropertyType == category);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(l => l.Title.Contains(q) || l.Description.Contains(q) ||
                                     l.City.Contains(q)  || l.District.Contains(q));

        query = sort switch
        {
            "newest"     => query.OrderByDescending(l => l.CreatedAt),
            "price_asc"  => query.OrderBy(l => l.Price),
            "price_desc" => query.OrderByDescending(l => l.Price),
            _            => query.OrderByDescending(l => l.ViewsCount),
        };

        var items      = await query.Take(60).ToListAsync(ct);
        var categories = await _db.DiscoveryCategories.AsNoTracking().ToListAsync(ct);

        return this.OkEnvelope("home.explore",
            items.Select(l => MapSummary(l, categories)).ToList());
    }

    // ── /home/search/suggestions ───────────────────────────────────────────
    [HttpGet("/home/search/suggestions")]
    public IActionResult Suggestions() =>
        this.OkEnvelope("home.search.suggestions", new {
            recent  = Array.Empty<string>(),
            popular = new[] { "صنعاء", "فيلا", "شقة مفروشة", "مكتب", "استراحة" }
        });

    // ── /listings ──────────────────────────────────────────────────────────
    [HttpGet("/listings")]
    public async Task<IActionResult> Listings(
        [FromQuery] string? city, [FromQuery] string? district,
        [FromQuery] string? propertyType, [FromQuery] string? timeUnit,
        [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice,
        [FromQuery] string? q,
        [FromQuery] string? sort,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
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
                                     l.City.Contains(q)  || l.District.Contains(q));

        var total = await query.CountAsync(ct);
        query = sort switch
        {
            "price_asc"  => query.OrderBy(l => l.Price),
            "price_desc" => query.OrderByDescending(l => l.Price),
            _            => query.OrderByDescending(l => l.ViewsCount),
        };
        var items      = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var categories = await _db.DiscoveryCategories.AsNoTracking().ToListAsync(ct);

        return this.OkEnvelope("listing.list", new {
            total, page, pageSize,
            items = items.Select(l => MapSummary(l, categories))
        });
    }

    // ── /listings/{id} ─────────────────────────────────────────────────────
    [HttpGet("/listings/{id:guid}")]
    public async Task<IActionResult> ListingDetails(Guid id, CancellationToken ct)
    {
        var l = await _db.Listings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return this.NotFoundEnvelope("listing_not_found");

        var owner      = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == l.OwnerId, ct);
        var categories = await _db.DiscoveryCategories.AsNoTracking().ToListAsync(ct);

        return this.OkEnvelope("listing.details", new {
            id = l.Id, title = l.Title, description = l.Description,
            price = l.Price, timeUnit = l.TimeUnit, propertyType = l.PropertyType,
            propertyTypeLabel = categories.FirstOrDefault(c => c.Slug == l.PropertyType)?.Label ?? l.PropertyType,
            city = l.City, district = l.District,
            lat = l.Lat, lng = l.Lng,
            bedroomCount = l.BedroomCount, bathroomCount = l.BathroomCount, areaSqm = l.AreaSqm,
            isVerified = l.IsVerified, viewsCount = l.ViewsCount,
            images = l.ImagesCsv?.Split('|', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>(),
            // ownerId على المستوى العلويّ ليستهلكه ListingDetails مباشرةً ويخفي
            // زرّ "ابدأ محادثة" عندما المالك = المستخدم الحاليّ.
            ownerId = l.OwnerId.ToString(),
            owner = owner is null ? null : new {
                id = owner.Id,
                name = owner.FullName,
                memberSince = owner.MemberSince
            }
        });
    }

    // ── /cities ─ /amenities ─ /categories (bridges over Discovery Kit) ────
    [HttpGet("/cities")]
    public async Task<IActionResult> Cities(CancellationToken ct) =>
        this.OkEnvelope("city.list",
            await _db.DiscoveryRegions.AsNoTracking().Where(r => r.Level == 1)
                .Select(r => r.Name).ToListAsync(ct));

    [HttpGet("/amenities")]
    public async Task<IActionResult> Amenities(CancellationToken ct) =>
        this.OkEnvelope("amenity.list",
            await _db.DiscoveryAmenities.AsNoTracking()
                .Select(a => new { key = a.Slug, label = a.Label }).ToListAsync(ct));

    [HttpGet("/categories")]
    public async Task<IActionResult> Categories(CancellationToken ct) =>
        this.OkEnvelope("category.list",
            await _db.DiscoveryCategories.AsNoTracking()
                .Select(c => new { id = c.Slug, label = c.Label, icon = c.Icon, kind = c.Kind })
                .ToListAsync(ct));

    // ── /plans (subscription catalog) ──────────────────────────────────────
    [HttpGet("/plans")]
    public async Task<IActionResult> Plans(CancellationToken ct) =>
        this.OkEnvelope("plan.list",
            await _db.Plans.AsNoTracking()
                .Select(p => new {
                    id = p.Id, label = p.Label, price = p.Price,
                    cycleLabel = p.CycleLabel,
                    maxActiveListings = p.MaxActiveListings,
                    maxFeaturedListings = p.MaxFeaturedListings,
                    maxImagesPerListing = p.MaxImagesPerListing,
                    isRecommended = p.IsRecommended,
                    description = p.Description,
                    features = string.IsNullOrEmpty(p.FeaturesCsv)
                        ? Array.Empty<string>()
                        : p.FeaturesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                })
                .ToListAsync(ct));

    // ── /legal ─────────────────────────────────────────────────────────────
    [HttpGet("/legal")]
    public IActionResult Legal() =>
        this.OkEnvelope("legal.list", new[]
        {
            new { key = "terms",    label = "الشروط والأحكام" },
            new { key = "privacy",  label = "سياسة الخصوصية" },
            new { key = "refund",   label = "سياسة الاسترداد" },
        });

    // ── helper ─────────────────────────────────────────────────────────────
    private static object MapSummary(ListingEntity l, IReadOnlyList<DiscoveryCategory> categories) => new
    {
        id = l.Id, title = l.Title,
        price = l.Price, timeUnit = l.TimeUnit,
        propertyType = l.PropertyType,
        propertyTypeLabel = categories.FirstOrDefault(c => c.Slug == l.PropertyType)?.Label ?? l.PropertyType,
        city = l.City, district = l.District,
        bedroomCount = l.BedroomCount, areaSqm = l.AreaSqm,
        isVerified = l.IsVerified, viewsCount = l.ViewsCount,
        firstImage = l.ImagesCsv?.Split('|').FirstOrDefault()
    };
}
