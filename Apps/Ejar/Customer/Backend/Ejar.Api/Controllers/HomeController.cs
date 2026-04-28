using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.OperationEngine.DataInterceptors;
using ACommerce.OperationEngine.Patterns;
using ACommerce.Kits.Discovery.Domain;
using ACommerce.Favorites.Operations.Entities;
using Ejar.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Controllers;

/// <summary>
/// نقاط نهاية عامة — تتبع نهج التاجات القياسية للتواصل مع المعترض العام CRUD.
/// </summary>
[ApiController, Route("api")]
public class HomeController : ControllerBase
{
    private readonly OpEngine _engine;

    public HomeController(OpEngine engine)
    {
        _engine = engine;
    }

    [HttpGet("listings")]
    public async Task<IActionResult> Listings(
        [FromQuery] string? city,
        [FromQuery] string? district,
        [FromQuery] string? propertyType,
        [FromQuery] string? timeUnit,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? q,
        [FromQuery] double? lat, [FromQuery] double? lng, [FromQuery] double? radius,
        [FromQuery] string? sort,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        // استخدام التاجات القياسية
        var op = Entry.Create("listing.list")
            .Tag(OperationTags.DbAction, DataOperationTypes.ReadAll)
            .Tag(OperationTags.TargetEntity, nameof(ListingEntity))
            .Build();

        var catOp = Entry.Create("aux.categories")
            .Tag(OperationTags.DbAction, DataOperationTypes.ReadAll)
            .Tag(OperationTags.TargetEntity, nameof(DiscoveryCategory))
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, async ctx => {
            var items = ctx.Get<IReadOnlyList<ListingEntity>>("db_result") ?? new List<ListingEntity>();
            
            var catResult = await _engine.ExecuteAsync(catOp);
            var categories = catResult.Context!.Get<IReadOnlyList<DiscoveryCategory>>("db_result") ?? new List<DiscoveryCategory>();

            // تصفية محلية
            var filtered = items.Where(l => l.Status == 1 &&
                 (string.IsNullOrEmpty(city) || l.City.Contains(city)) &&
                 (string.IsNullOrEmpty(district) || l.District.Contains(district)) &&
                 (string.IsNullOrEmpty(propertyType) || l.PropertyType == propertyType) &&
                 (string.IsNullOrEmpty(timeUnit) || l.TimeUnit == timeUnit) &&
                 (!minPrice.HasValue || l.Price >= minPrice.Value) &&
                 (!maxPrice.HasValue || l.Price <= maxPrice.Value) &&
                 (string.IsNullOrEmpty(q) || l.Title.Contains(q!) || l.Description.Contains(q!) || l.City.Contains(q!) || l.District.Contains(q!))
            ).ToList();

            var total = filtered.Count;
            
            filtered = sort switch {
                "price_asc"  => filtered.OrderBy(l => l.Price).ToList(),
                "price_desc" => filtered.OrderByDescending(l => l.Price).ToList(),
                _            => filtered.OrderByDescending(l => l.ViewsCount).ToList()
            };

            var pagedItems = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var favIds = new List<Guid>();

            return new {
                total, page, pageSize,
                items = pagedItems.Select(l => MapSummary(l, favIds, categories))
            };
        });

        return Ok(env);
    }

    [HttpGet("home/view")]
    public async Task<IActionResult> HomeView([FromQuery] string? city = null)
    {
        var op = Entry.Create("home.view")
            .Tag(OperationTags.DbAction, DataOperationTypes.ReadAll)
            .Tag(OperationTags.TargetEntity, nameof(ListingEntity))
            .Build();

        var catOp = Entry.Create("home.cats")
            .Tag(OperationTags.DbAction, DataOperationTypes.ReadAll)
            .Tag(OperationTags.TargetEntity, nameof(DiscoveryCategory))
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, async ctx => {
            var items = ctx.Get<IReadOnlyList<ListingEntity>>("db_result") ?? new List<ListingEntity>();
            
            var catRes = await _engine.ExecuteAsync(catOp);
            var categories = catRes.Context!.Get<IReadOnlyList<DiscoveryCategory>>("db_result") ?? new List<DiscoveryCategory>();

            var filtered = items.Where(l => l.Status == 1 && (string.IsNullOrEmpty(city) || l.City == city)).ToList();
            var favIds = new List<Guid>();

            return new {
                categories = categories.Select(c => new { id = c.Slug, label = c.Label, icon = c.Icon }),
                featured = filtered.Where(l => l.IsVerified).Select(l => MapSummary(l, favIds, categories)).ToList(),
                @new = filtered.Where(l => !l.IsVerified).Take(6).Select(l => MapSummary(l, favIds, categories)).ToList(),
                city
            };
        });

        return Ok(env);
    }

    // ملاحظة: GET /version/check يُقدَّم من Versions Kit (VersionsController).
    // أُزيل من هنا تجنّباً للتكرار ولتفعيل آليّة الإصدارات الموحَّدة.

    private static object MapSummary(ListingEntity l, List<Guid> favIds, IReadOnlyList<DiscoveryCategory> categories) => new
    {
        id = l.Id, title = l.Title,
        price = l.Price, timeUnit = l.TimeUnit,
        propertyType = l.PropertyType,
        propertyTypeLabel = categories.FirstOrDefault(c => c.Slug == l.PropertyType)?.Label ?? l.PropertyType,
        city = l.City, district = l.District,
        bedroomCount = l.BedroomCount, areaSqm = l.AreaSqm,
        isVerified = l.IsVerified, viewsCount = l.ViewsCount,
        isFavorite = favIds.Contains(l.Id),
        firstImage = l.ImagesCsv?.Split(',').FirstOrDefault()
    };
}
