using ACommerce.OperationEngine.Wire.Http;
using Ashare.V3.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// يَتَجاوَز <c>/categories</c> الكيت الافتِراضي (الذي يَقرَأ
/// <see cref="ACommerce.Kits.Discovery.Domain.DiscoveryCategory"/> الفارِغ
/// في V3) بِقِراءَة مِن جَدول <c>TaxonomyNodes</c> — نَفس المَصدَر الَّذي
/// تَستَخدِمه <c>/taxonomy/listing_categories</c>. هذا يَضمَن أَنّ كُلّ
/// الصَّفَحات (Home, Explore, CreateListing, Search) تَرى نَفس الفِئات،
/// ولوحَة الإدارَة المُستَقبَلِيَّة تُعَدِّل مَكاناً واحِداً.
///
/// <para><b>التَّحويل</b>: <c>TaxonomyNode</c> ⇒ wire shape الـ Discovery
/// kit: <c>{ id = Code, label = NameAr ?? Name, icon = Icon, kind = "category" }</c>.
/// نَستَخدِم الـ leaves فَقَط (<c>ParentId != null</c>) لِأَنّ المُستَخدِم
/// يَختار نَوع إعلان مَحَدَّد، لا الفِئَة الأَب.</para>
/// </summary>
[ApiController]
public sealed class CategoriesController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    public CategoriesController(AshareV3DbContext db) => _db = db;

    [HttpGet("/categories", Order = -10)]   // يَفوز عَلى kit's Discovery.Categories
    public async Task<IActionResult> List(CancellationToken ct)
    {
        const string root = "listing_categories";
        var rows = await _db.TaxonomyNodes.AsNoTracking()
            .Where(t => t.RootCode == root && t.IsActive && t.ParentId != null)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Code)
            .Select(t => new
            {
                id    = t.Code,
                label = t.NameAr ?? t.Name,
                icon  = t.Icon ?? "tag",
                kind  = "category",
            })
            .ToListAsync(ct);
        return this.OkEnvelope("discovery.categories.list", rows);
    }
}
