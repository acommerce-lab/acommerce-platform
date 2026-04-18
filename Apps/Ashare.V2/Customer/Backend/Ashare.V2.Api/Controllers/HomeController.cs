using ACommerce.OperationEngine.Wire.Http;
using Ashare.V2.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.V2.Api.Controllers;

/// <summary>
/// مُعرِّف الصفحة الرئيسية + قوائم الإعلانات + البحث.
/// كل استجابة = OperationEnvelope.
/// </summary>
[ApiController]
[Route("home")]
public class HomeController : ControllerBase
{
    private static IEnumerable<object> CategoriesDto() =>
        AshareV2Seed.Categories.Select(c => new { id = c.Id, label = c.Label, icon = c.Icon });

    private static object ToRow(AshareV2Seed.ListingSeed l) => new
    {
        id = l.Id,
        title = l.Title,
        description = l.Description,
        price = l.Price,
        currency = "SAR",
        timeUnit = l.TimeUnit,
        city = l.City,
        district = l.District,
        lat = l.Lat,
        lng = l.Lng,
        categoryName = AshareV2Seed.Categories.FirstOrDefault(c => c.Id == l.CategoryId)?.Label,
        status = 1,
        isFeatured = l.IsFeatured,
        viewCount = 0,
        thumbnailUrl = (string?)null,
        ownerName = (string?)null,
        ownerAvatarUrl = (string?)null,
        capacity = l.Capacity,
        rating = l.Rating
    };

    [HttpGet("view")]
    public IActionResult View()
    {
        var featured = AshareV2Seed.Listings.Where(l => l.IsFeatured).Select(ToRow).ToList();
        var @new     = AshareV2Seed.Listings.Where(l => !l.IsFeatured).Take(6).Select(ToRow).ToList();
        return this.OkEnvelope("home.view", new
        {
            categories = CategoriesDto(),
            featured,
            @new
        });
    }

    [HttpGet("explore")]
    public IActionResult Explore(
        [FromQuery] string? category,
        [FromQuery] decimal? priceMin,
        [FromQuery] decimal? priceMax,
        [FromQuery] int? capacity,
        [FromQuery] int? minRating,
        [FromQuery] string? sort = "newest")
    {
        IEnumerable<AshareV2Seed.ListingSeed> q = AshareV2Seed.Listings;

        if (!string.IsNullOrEmpty(category)) q = q.Where(l => l.CategoryId == category);
        if (priceMin.HasValue)  q = q.Where(l => l.Price >= priceMin.Value);
        if (priceMax.HasValue)  q = q.Where(l => l.Price <= priceMax.Value);
        if (capacity.HasValue && capacity.Value > 0)
        {
            var cap = capacity.Value;
            q = q.Where(l =>
            {
                var c = l.Capacity;
                return cap switch
                {
                    5  => c is >= 1  and <= 5,
                    10 => c is >= 6  and <= 10,
                    20 => c is >= 11 and <= 20,
                    50 => c >= 20,
                    _  => true
                };
            });
        }
        if (minRating.HasValue && minRating.Value > 0)
            q = q.Where(l => l.Rating >= minRating.Value);

        q = sort switch
        {
            "price_low"  => q.OrderBy(l => l.Price),
            "price_high" => q.OrderByDescending(l => l.Price),
            "rating"     => q.OrderByDescending(l => l.Rating),
            "capacity"   => q.OrderByDescending(l => l.Capacity),
            _            => q
        };

        return this.OkEnvelope("catalog.list", q.Select(ToRow).ToList());
    }

    [HttpGet("space/{id}")]
    public IActionResult SpaceDetails(string id)
    {
        var l = AshareV2Seed.Listings.FirstOrDefault(x => x.Id == id);
        if (l is null) return this.NotFoundEnvelope("listing_not_found", $"Listing '{id}' not found");

        var dto = new
        {
            id = l.Id,
            title = l.Title,
            description = l.Description,
            images = Array.Empty<string>(),  // لا صور حقيقيّة في الـ seed
            locationText = $"{l.City} — {l.District}",
            capacity = l.Capacity,
            rating = l.Rating,
            priceDisplay = $"{l.Price:0} SAR",
            priceUnit = UnitLabel(l.TimeUnit),
            amenities = l.Amenities.Select(a => new { key = a, label = AmenityLabel(a) }).ToList(),
            owner = new { name = "مالك عشير", memberSince = "2024" }
        };
        return this.OkEnvelope("listing.details", dto);
    }

    [HttpGet("search/suggestions")]
    public IActionResult SearchSuggestions() =>
        this.OkEnvelope("search.suggestions", new
        {
            popular = AshareV2Seed.PopularSearches,
            quickFilters = AshareV2Seed.QuickFilters.Select(q => new { id = q.Id, label = q.Label, icon = q.Icon })
        });

    [HttpGet("notifications")]
    public IActionResult Notifications() =>
        this.OkEnvelope("notification.list",
            AshareV2Seed.Notifications.Select(n => new
            {
                id = n.Id,
                title = n.Title,
                body = n.Body,
                type = n.Type,
                isRead = n.IsRead,
                createdAt = n.CreatedAt
            }).ToList());

    private static string UnitLabel(string u) => u switch
    {
        "day"   => "/ يوم",
        "night" => "/ ليلة",
        "week"  => "/ أسبوع",
        "month" => "/ شهر",
        "year"  => "/ سنة",
        _       => ""
    };

    private static string AmenityLabel(string key) => key switch
    {
        "wifi"    => "واي فاي",
        "ac"      => "تكييف",
        "kitchen" => "مطبخ",
        "parking" => "موقف سيّارات",
        "laundry" => "غسّالة",
        _         => key
    };
}
