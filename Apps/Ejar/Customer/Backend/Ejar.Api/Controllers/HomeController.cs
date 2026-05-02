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

    // ── /home/search/suggestions ───────────────────────────────────────────
    [HttpGet("/home/search/suggestions")]
    public IActionResult Suggestions() =>
        this.OkEnvelope("home.search.suggestions", new {
            recent  = Array.Empty<string>(),
            popular = new[] { "إب", "فيلا", "شقة مفروشة", "مكتب", "استراحة" }
        });

    // ── ملاحظة: /listings و /listings/{id} و /home/explore نُقلت لـ
    //   Listings.Backend kit (ListingsController).
    //   /cities و /amenities و /categories → Discovery.Backend.
    //   /plans → Subscriptions.Backend.
    //   ما تبقّى هنا = الـ "home composition" + suggestions + legal.

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
