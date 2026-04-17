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

    private static readonly List<object> _featured =
    [
        new { id = "L-101", title = "شقة مفروشة في حي النرجس", description = (string?)null, price = 2500m, currency = "SAR", timeUnit = "month", city = "الرياض", district = "النرجس", categoryName = (string?)null, status = 1, isFeatured = true,  viewCount = 0, thumbnailUrl = (string?)null, ownerName = (string?)null, ownerAvatarUrl = (string?)null },
        new { id = "L-102", title = "غرفة في شقة طلاب",        description = (string?)null, price =  900m, currency = "SAR", timeUnit = "month", city = "جدة",    district = "السلامة", categoryName = (string?)null, status = 1, isFeatured = true,  viewCount = 0, thumbnailUrl = (string?)null, ownerName = (string?)null, ownerAvatarUrl = (string?)null },
        new { id = "L-103", title = "استديو قرب جامعة الملك سعود", description = (string?)null, price = 1800m, currency = "SAR", timeUnit = "month", city = "الرياض", district = "الدرعية", categoryName = (string?)null, status = 1, isFeatured = true,  viewCount = 0, thumbnailUrl = (string?)null, ownerName = (string?)null, ownerAvatarUrl = (string?)null }
    ];

    private static readonly List<object> _new =
    [
        new { id = "L-201", title = "سكن عائلي في المزاحمية", description = (string?)null, price = 3200m, currency = "SAR", timeUnit = "month", city = "الرياض", district = "المزاحمية", categoryName = (string?)null, status = 1, isFeatured = false, viewCount = 0, thumbnailUrl = (string?)null, ownerName = (string?)null, ownerAvatarUrl = (string?)null },
        new { id = "L-202", title = "شقة يومي قرب الحرم",      description = (string?)null, price =  350m, currency = "SAR", timeUnit = "day",   city = "مكة",    district = "العزيزية", categoryName = (string?)null, status = 1, isFeatured = false, viewCount = 0, thumbnailUrl = (string?)null, ownerName = (string?)null, ownerAvatarUrl = (string?)null },
        new { id = "L-203", title = "غرفة في فيلا مشتركة",     description = (string?)null, price = 1200m, currency = "SAR", timeUnit = "month", city = "الدمام", district = "الشاطئ",   categoryName = (string?)null, status = 1, isFeatured = false, viewCount = 0, thumbnailUrl = (string?)null, ownerName = (string?)null, ownerAvatarUrl = (string?)null },
        new { id = "L-204", title = "استديو في شمال الرياض",   description = (string?)null, price = 2100m, currency = "SAR", timeUnit = "month", city = "الرياض", district = "الصحافة",  categoryName = (string?)null, status = 1, isFeatured = false, viewCount = 0, thumbnailUrl = (string?)null, ownerName = (string?)null, ownerAvatarUrl = (string?)null }
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
