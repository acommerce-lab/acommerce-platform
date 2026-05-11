using ACommerce.OperationEngine.Wire.Http;
using Ashare.V3.Data;
using Ashare.V3.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// نُقَط النِّهايَة العامَّة لِـ /home/* (V1-compat shape). نَظير
/// <c>Ejar.Api.Controllers.HomeController</c> — التَّطبيق يَحتاجها لِأَنّ
/// الـ frontend (Customer.Marketplace.Home) يَستَدعي <c>/home/view</c>،
/// <c>/home/explore</c>، <c>/home/search/suggestions</c>، <c>/legal</c>
/// بِشَكل JSON V1 المُسَطَّح (firstImage, propertyTypeLabel, isFavorite، …)
/// لا يَتَطابَق مَع <c>/listings</c> الَّذي يَرُدّه kit (IListing pure).
///
/// <para><b>الفَرق عَن إيجار</b>: نَقرَأ <see cref="ProductListingEntity"/>
/// (asharedb schema): <c>VendorId</c>، <c>IsFeatured</c>، <c>FeaturedImage</c>،
/// <c>ImagesJson</c> (JSON array)، <c>Condition</c> بَدَل <c>PropertyType</c>،
/// <c>Address</c> بَدَل <c>District</c>، <c>Latitude/Longitude</c> بَدَل
/// <c>Lat/Lng</c>، <c>IsActive</c> bool بَدَل <c>Status</c> int.</para>
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
        var listings = await _db.ProductListings.AsNoTracking()
            .Where(l => l.IsActive && (string.IsNullOrEmpty(city) || l.City == city))
            .ToListAsync(ct);
        var categories = await _db.DiscoveryCategories.AsNoTracking().ToListAsync(ct);

        return this.OkEnvelope("home.view", new
        {
            categories = categories.Select(c => new { id = c.Slug, label = c.Label, icon = c.Icon }),
            featured   = listings.Where(l => l.IsFeatured).Select(l => MapSummary(l, categories)).ToList(),
            @new       = listings.Where(l => !l.IsFeatured).Take(6).Select(l => MapSummary(l, categories)).ToList(),
            city
        });
    }

    // ── /home/explore (V1 legacy shape) ─────────────────────────────────────
    [HttpGet("/home/explore")]
    public async Task<IActionResult> Explore(
        [FromQuery] string? city,
        [FromQuery] string? category,        // alias لِـ condition
        [FromQuery] string? propertyType,
        [FromQuery] string? q,
        [FromQuery(Name = "minPrice")] decimal? minPrice = null,
        [FromQuery(Name = "maxPrice")] decimal? maxPrice = null,
        [FromQuery] string? sort = null,
        CancellationToken ct = default)
    {
        var typeFilter = propertyType ?? category;
        var query = _db.ProductListings.AsNoTracking().Where(l => l.IsActive);
        if (!string.IsNullOrWhiteSpace(city))       query = query.Where(l => l.City == city);
        if (!string.IsNullOrWhiteSpace(typeFilter)) query = query.Where(l => l.Condition == typeFilter);
        if (minPrice is { } minP)                   query = query.Where(l => l.Price >= minP);
        if (maxPrice is { } maxP)                   query = query.Where(l => l.Price <= maxP);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(l =>
                l.Title.Contains(s)             ||
                (l.Description != null && l.Description.Contains(s)) ||
                (l.City        != null && l.City.Contains(s))        ||
                (l.Address     != null && l.Address.Contains(s)));
        }
        query = sort switch
        {
            "newest"     => query.OrderByDescending(l => l.CreatedAt),
            "price_asc"  => query.OrderBy(l => l.Price),
            "price_desc" => query.OrderByDescending(l => l.Price),
            _            => query.OrderByDescending(l => l.IsFeatured).ThenByDescending(l => l.CreatedAt),
        };

        var rows = await query.Take(60).ToListAsync(ct);
        var categories = await _db.DiscoveryCategories.AsNoTracking().ToListAsync(ct);

        var items = rows.Select(l => new
        {
            id                = l.Id,
            title             = l.Title,
            price             = l.Price,
            timeUnit          = "fixed",
            timeUnitLabel     = "",
            propertyType      = l.Condition ?? "",
            propertyTypeLabel = categories.FirstOrDefault(c => c.Slug == l.Condition)?.Label ?? l.Condition ?? "",
            city              = l.City ?? "",
            district          = l.Address ?? "",
            lat               = l.Latitude  ?? 0,
            lng               = l.Longitude ?? 0,
            bedroomCount      = 0,
            areaSqm           = 0,
            isVerified        = l.IsFeatured,
            viewsCount        = l.ViewCount,
            isFavorite        = false,
            amenities         = Array.Empty<string>(),
            firstImage        = l.FeaturedImage ?? FirstFromJson(l.ImagesJson),
        }).ToList();

        return this.OkEnvelope("home.explore", items);
    }

    // ── /home/search/suggestions ───────────────────────────────────────────
    [HttpGet("/home/search/suggestions")]
    public IActionResult Suggestions() =>
        this.OkEnvelope("home.search.suggestions", new
        {
            recent  = Array.Empty<string>(),
            popular = new[] { "إب", "صنعاء", "عدن", "جديد", "مستعمل" }
        });

    // ── /legal ─────────────────────────────────────────────────────────────
    [HttpGet("/legal")]
    public IActionResult Legal() =>
        this.OkEnvelope("legal.list", new[]
        {
            new { key = "terms",   label = "الشروط والأحكام" },
            new { key = "privacy", label = "سياسة الخصوصية"  },
            new { key = "refund",  label = "سياسة الاسترداد" },
        });

    // ── helpers ────────────────────────────────────────────────────────────
    private static object MapSummary(ProductListingEntity l, IReadOnlyList<ACommerce.Kits.Discovery.Domain.DiscoveryCategory> categories) => new
    {
        id           = l.Id,
        title        = l.Title,
        price        = l.Price,
        timeUnit     = "fixed",
        propertyType = l.Condition ?? "",
        propertyTypeLabel = categories.FirstOrDefault(c => c.Slug == l.Condition)?.Label ?? l.Condition ?? "",
        city         = l.City    ?? "",
        district     = l.Address ?? "",
        bedroomCount = 0,
        areaSqm      = 0,
        isVerified   = l.IsFeatured,
        viewsCount   = l.ViewCount,
        firstImage   = l.FeaturedImage ?? FirstFromJson(l.ImagesJson),
    };

    private static string? FirstFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var arr = JsonSerializer.Deserialize<string[]>(json);
            return arr is { Length: > 0 } ? arr[0] : null;
        }
        catch { return null; }
    }
}
