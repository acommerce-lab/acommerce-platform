using ACommerce.OperationEngine.Wire.Http;
using Ashare.V3.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// يَتَجاوَز <c>/categories</c> الكيت الافتِراضي (الذي يَقرَأ
/// <see cref="ACommerce.Kits.Discovery.Domain.DiscoveryCategory"/> الفارِغ
/// في V3) بِقِراءَة مِن جَدول <c>ProductCategory</c> الإنتاجِي. نَفس wire
/// shape (<c>{id, label, icon, kind}</c>) ⇒ store الواجِهَة لا يَحتاج تَعديل.
///
/// <para><b>الفِئات</b>: <c>id = Slug</c> (مَفتاح ثابِت)،
/// <c>label = Name</c>، <c>icon = Icon</c>، <c>kind = "category"</c>
/// (الكيت يُمَيِّز بَين أَنواع، V3 يَستَخدِم نَوعاً واحِداً).</para>
/// </summary>
[ApiController]
public sealed class CategoriesController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    public CategoriesController(AshareV3DbContext db) => _db = db;

    [HttpGet("/categories")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _db.ProductCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new
            {
                id    = c.Slug,
                label = c.Name,
                icon  = c.Icon ?? "tag",
                kind  = "category",
            })
            .ToListAsync(ct);
        return this.OkEnvelope("discovery.categories.list", rows);
    }
}
