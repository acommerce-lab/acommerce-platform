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
    // اسم البارامترَين priceMin/priceMax مطابق لما يبنيه Explore.razor
    // BuildQueryString — لا تُغيِّره لئلّا تنكسر الواجهة. /listings يقبل
    // minPrice/maxPrice (اسم REST قياسيّ)، نقبل هنا الاسمين معاً.
    [HttpGet("/home/explore")]
    public async Task<IActionResult> HomeExplore(
        [FromQuery] string? sort,
        [FromQuery] string? category,
        [FromQuery] string? q,
        [FromQuery] string? city,
        [FromQuery] string? district,
        [FromQuery] string? propertyType,
        [FromQuery] string? timeUnit,
        [FromQuery(Name = "priceMin")] decimal? priceMin,
        [FromQuery(Name = "priceMax")] decimal? priceMax,
        [FromQuery(Name = "minPrice")] decimal? minPriceAlias,
        [FromQuery(Name = "maxPrice")] decimal? maxPriceAlias,
        CancellationToken ct = default)
    {
        var min = priceMin ?? minPriceAlias;
        var max = priceMax ?? maxPriceAlias;

        var query = _db.Listings.AsNoTracking().Where(l => l.Status == 1);
        if (!string.IsNullOrWhiteSpace(category))     query = query.Where(l => l.PropertyType == category);
        if (!string.IsNullOrWhiteSpace(propertyType)) query = query.Where(l => l.PropertyType == propertyType);
        if (!string.IsNullOrWhiteSpace(timeUnit))     query = query.Where(l => l.TimeUnit == timeUnit);
        if (!string.IsNullOrWhiteSpace(city))         query = query.Where(l => l.City.Contains(city));
        if (!string.IsNullOrWhiteSpace(district))     query = query.Where(l => l.District.Contains(district));
        if (min.HasValue)                             query = query.Where(l => l.Price >= min.Value);
        if (max.HasValue)                             query = query.Where(l => l.Price <= max.Value);
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
            popular = new[] { "إب", "فيلا", "شقة مفروشة", "مكتب", "استراحة" }
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

    // /cities ─ /amenities ─ /categories  → نُقلت لـ Discovery.Backend kit
    // (بلا prefix). لا حاجة لـ bridge هنا — الكيت يكشفها مباشرةً.

    // /plans → نُقلت لـ Subscriptions.Backend kit (PlansController).

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
        // المُصغّر للبطاقات (~30KB)؛ fallback على أوّل صورة في ImagesCsv
        // للإعلانات القديمة قبل إضافة ThumbnailUrl.
        firstImage = l.ThumbnailUrl ?? l.ImagesCsv?.Split('|').FirstOrDefault()
    };
}
