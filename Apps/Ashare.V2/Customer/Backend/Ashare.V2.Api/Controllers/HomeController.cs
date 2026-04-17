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
}
