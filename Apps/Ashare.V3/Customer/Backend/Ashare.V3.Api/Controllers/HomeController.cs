using ACommerce.Favorites.Operations.Entities;
using ACommerce.Kits.Discovery.Domain;
using ACommerce.OperationEngine.Wire.Http;
using Ashare.V3.Data;
using Ashare.V3.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Api.Controllers;

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
    private readonly AshareV3DbContext _db;
    public HomeController(AshareV3DbContext db) => _db = db;

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

    // ── /home/explore (V1 legacy shape: List<ListingDto> مَسطَّحة) ───────
    // V1 (Apps/AshareV3/Customer/Shared/Ashare.V3.Customer.UI/Components/Pages/Explore.razor)
    // يَتَوَقَّع <c>data: List&lt;ListingDto&gt;</c> مع الحقول الكاملة
    // (firstImage, propertyTypeLabel, isFavorite, amenities). الـ kit الجديد
    // (ListingsController.Search) يُرجع <c>data: { total, page, pageSize, items }</c>
    // مع IListing فقط (لا labels ولا firstImage) — كَسَر V1.
    //
    // هذا الـ endpoint يَبقى لـ V1 backward-compat. V2 + apps حديثة
    // تَستَهلِك /listings مباشرة.
    [HttpGet("/home/explore")]
    public async Task<IActionResult> Explore(
        [FromQuery] string? city,
        [FromQuery] string? category,        // alias لـ propertyType
        [FromQuery] string? propertyType,
        [FromQuery] string? q,
        [FromQuery] int minBedrooms = 0,
        [FromQuery(Name = "minPrice")]  decimal? minPrice  = null,
        [FromQuery(Name = "maxPrice")]  decimal? maxPrice  = null,
        [FromQuery] string? sort = null,
        CancellationToken ct = default)
    {
        var typeFilter = propertyType ?? category;
        var query = _db.Listings.AsNoTracking().Where(l => l.Status == 1);
        if (!string.IsNullOrWhiteSpace(city))         query = query.Where(l => l.City == city);
        if (!string.IsNullOrWhiteSpace(typeFilter))   query = query.Where(l => l.PropertyType == typeFilter);
        if (minPrice is { } minP)                     query = query.Where(l => l.Price >= minP);
        if (maxPrice is { } maxP)                     query = query.Where(l => l.Price <= maxP);
        if (minBedrooms > 0)                          query = query.Where(l => l.BedroomCount >= minBedrooms);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(l =>
                l.Title.Contains(s) || l.Description.Contains(s) ||
                l.City.Contains(s)  || l.District.Contains(s));
        }
        query = sort switch
        {
            "newest"     => query.OrderByDescending(l => l.CreatedAt),
            "price_asc"  => query.OrderBy(l => l.Price),
            "price_desc" => query.OrderByDescending(l => l.Price),
            _            => query.OrderByDescending(l => l.IsVerified).ThenByDescending(l => l.CreatedAt),
        };

        var rows = await query.Take(60).ToListAsync(ct);
        var categories = await _db.DiscoveryCategories.AsNoTracking().ToListAsync(ct);

        var items = rows.Select(l => new
        {
            id                = l.Id,
            title             = l.Title,
            price             = l.Price,
            timeUnit          = l.TimeUnit,
            timeUnitLabel     = l.TimeUnit switch
            {
                "monthly" => "شهرياً",
                "yearly"  => "سنوياً",
                "daily"   => "يومياً",
                _ => l.TimeUnit,
            },
            propertyType      = l.PropertyType,
            propertyTypeLabel = categories.FirstOrDefault(c => c.Slug == l.PropertyType)?.Label ?? l.PropertyType,
            city              = l.City,
            district          = l.District,
            lat               = l.Lat,
            lng               = l.Lng,
            bedroomCount      = l.BedroomCount,
            areaSqm           = l.AreaSqm,
            isVerified        = l.IsVerified,
            viewsCount        = l.ViewsCount,
            isFavorite        = false,           // V1 يَملأها من FavoriteListingIds محلّياً
            amenities         = (string?[])Array.Empty<string?>(),
            firstImage        = l.ThumbnailUrl
                              ?? l.ImagesCsv?.Split('|', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
        }).ToList();

        return this.OkEnvelope("home.explore", items);
    }

    // ── /home/search/suggestions ───────────────────────────────────────────
    [HttpGet("/home/search/suggestions")]
    public IActionResult Suggestions() =>
        this.OkEnvelope("home.search.suggestions", new {
            recent  = Array.Empty<string>(),
            popular = new[] { "إب", "فيلا", "شقة مفروشة", "مكتب", "استراحة" }
        });

    // ── ملاحظة: /listings و /listings/{id} في Listings.Backend kit.
    //   /home/explore يَبقى هنا (V1 legacy shape).
    //   /cities و /amenities و /categories → Discovery.Backend.
    //   /plans → Subscriptions.Backend.

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
