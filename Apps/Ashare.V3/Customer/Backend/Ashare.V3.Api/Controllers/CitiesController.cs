using ACommerce.OperationEngine.Wire.Http;
using Ashare.V3.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// يَستَبدِل <c>/cities</c> الكيت الافتِراضي (الذي يَقرَأ
/// <c>DiscoveryRegion</c> الفارِغ في V3) بِقِراءَة مِن جَدول <c>Cities</c>
/// الإنتاجِي. الـ ASP.NET routing يَختار الأَخير المُسَجَّل لِنَفس المَسار
/// — يَكفي أَن يَكون هذا الـ controller في تَطبيق V3 لِيَفوز.
///
/// <para><b>الشَكل المُرجَع</b>: قائِمَة أَسماء (strings) — مُطابِق
/// لِـ <c>DiscoveryController.Cities</c> ⇒ store الواجِهَة لا يَحتاج
/// تَغيير.</para>
/// </summary>
[ApiController]
public sealed class CitiesController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    public CitiesController(AshareV3DbContext db) => _db = db;

    [HttpGet("/cities")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var names = await _db.Cities.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => c.Name)
            .ToListAsync(ct);
        return this.OkEnvelope("discovery.cities.list", (IEnumerable<string>)names);
    }
}
