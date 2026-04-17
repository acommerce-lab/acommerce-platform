using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.V2.Api.Controllers;

/// <summary>
/// مُعرِّف الصفحة الرئيسية — يُرجع فئات + إعلانات مميّزة + إعلانات جديدة
/// في مغلف واحد (OperationEnvelope).
///
/// القانون 2: كل استجابة = OperationEnvelope. هذا endpoint قراءة فقط
/// (لا قيد محاسبي) لأن القراءة لا تغيّر الحالة.
/// </summary>
[ApiController]
[Route("home")]
public class HomeController : ControllerBase
{
    // Seed data — في الجلسة التالية نستبدلها بـ IRepository<Listing> + Entry.
    private static readonly List<object> _categories =
    [
        new { id = "apartment", label = "شقة", icon = "building" },
        new { id = "room",      label = "غرفة", icon = "home" },
        new { id = "studio",    label = "استديو", icon = "package" },
        new { id = "villa",     label = "فيلا", icon = "store" },
        new { id = "shared",    label = "مشترك", icon = "user" }
    ];

    private static object Listing(string id, string title, decimal price, string unit, string city, string district, bool featured, int capacity, decimal rating) =>
        new { id, title, description = (string?)null, price, currency = "SAR", timeUnit = unit, city, district, categoryName = (string?)null, status = 1, isFeatured = featured, viewCount = 0, thumbnailUrl = (string?)null, ownerName = (string?)null, ownerAvatarUrl = (string?)null, capacity, rating };

    private static readonly List<object> _featured =
    [
        Listing("L-101", "شقة مفروشة في حي النرجس",       2500m, "month", "الرياض", "النرجس",   true, 3, 4.5m),
        Listing("L-102", "غرفة في شقة طلاب",               900m, "month", "جدة",    "السلامة",  true, 4, 4.2m),
        Listing("L-103", "استديو قرب جامعة الملك سعود",   1800m, "month", "الرياض", "الدرعية",  true, 2, 4.8m)
    ];

    private static readonly List<object> _new =
    [
        Listing("L-201", "سكن عائلي في المزاحمية",        3200m, "month", "الرياض", "المزاحمية", false, 5, 4.0m),
        Listing("L-202", "شقة يومي قرب الحرم",             350m, "day",   "مكة",    "العزيزية",  false, 6, 4.7m),
        Listing("L-203", "غرفة في فيلا مشتركة",           1200m, "month", "الدمام", "الشاطئ",    false, 4, 4.3m),
        Listing("L-204", "استديو في شمال الرياض",         2100m, "month", "الرياض", "الصحافة",   false, 2, 4.1m)
    ];

    [HttpGet("view")]
    public IActionResult View() =>
        this.OkEnvelope("home.view", new
        {
            categories = _categories,
            featured = _featured,
            @new = _new
        });

    /// <summary>
    /// GET /home/explore — قائمة الإعلانات مع دعم التصفية والترتيب.
    /// الفلاتر اختياريّة؛ كلّها query string.
    /// </summary>
    [HttpGet("explore")]
    public IActionResult Explore(
        [FromQuery] string? category,
        [FromQuery] decimal? priceMin,
        [FromQuery] decimal? priceMax,
        [FromQuery] int? capacity,
        [FromQuery] int? minRating,
        [FromQuery] string? sort = "newest")
    {
        // أطرح كل الإعلانات ثم أصفّي؛ في الإنتاج يُنفَّذ على DB.
        var all = _featured.Concat(_new).Select(o => (dynamic)o).ToList();

        IEnumerable<dynamic> q = all;

        if (!string.IsNullOrEmpty(category))
        {
            // الفئة لم تُخزَّن على الـ seed بعد — مفلتر مبسَّط وفق المدينة مؤقتاً.
            // سيستقرّ في الشريحة 3 (SpaceDetails) مع ربط CategoryId حقيقي.
        }
        if (priceMin.HasValue) q = q.Where(x => (decimal)x.price >= priceMin.Value);
        if (priceMax.HasValue) q = q.Where(x => (decimal)x.price <= priceMax.Value);
        if (capacity.HasValue && capacity.Value > 0)
        {
            var cap = capacity.Value;
            q = q.Where(x =>
            {
                int c = (int)x.capacity;
                return cap switch
                {
                    5  => c >= 1  && c <= 5,
                    10 => c >= 6  && c <= 10,
                    20 => c >= 11 && c <= 20,
                    50 => c >= 20,
                    _  => true
                };
            });
        }
        if (minRating.HasValue && minRating.Value > 0)
            q = q.Where(x => (decimal)x.rating >= minRating.Value);

        q = sort switch
        {
            "price_low"  => q.OrderBy      (x => (decimal)x.price),
            "price_high" => q.OrderByDescending(x => (decimal)x.price),
            "rating"     => q.OrderByDescending(x => (decimal)x.rating),
            "capacity"   => q.OrderByDescending(x => (int)x.capacity),
            _            => q // "newest" — stays in seed order (new first if we extend)
        };

        return this.OkEnvelope("catalog.list", q.ToList());
    }
}
