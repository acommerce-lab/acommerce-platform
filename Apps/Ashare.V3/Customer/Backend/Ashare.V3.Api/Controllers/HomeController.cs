using ACommerce.OperationEngine.Wire.Http;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ashare.V3.Data;
using Ashare.V3.Data.Templates;
using Ashare.V3.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// نُقَط النِّهايَة <c>/home/*</c> لِـ V1-compat shape. مَع تَفعيل Template+
/// Snapshot: كُلّ بِطاقَة (Featured/New/Explore) تَحمِل <c>attributes</c>
/// (DynamicAttribute) بِناءً عَلى قالَب فِئَة الإعلان ⇒ AcSpaceCard يَرسُم
/// chips تِلقائيّاً.
/// </summary>
[ApiController]
public sealed class HomeController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    private readonly ProductionAttributeTemplateSource _prodSource;
    public HomeController(AshareV3DbContext db, ProductionAttributeTemplateSource prodSource)
    {
        _db = db;
        _prodSource = prodSource;
    }

    [HttpGet("/home/view")]
    public async Task<IActionResult> HomeView([FromQuery] string? city, CancellationToken ct)
    {
        var listings = await _db.ProductListings.AsNoTracking()
            .Where(l => l.IsActive && (string.IsNullOrEmpty(city) || l.City == city))
            .ToListAsync(ct);
        var categories = await _db.DiscoveryCategories.AsNoTracking().ToListAsync(ct);
        var templates  = await LoadTemplatesAsync(listings, ct);

        return this.OkEnvelope("home.view", new
        {
            categories = categories.Select(c => new { id = c.Slug, label = c.Label, icon = c.Icon }),
            featured   = listings.Where(l => l.IsFeatured)
                                 .Select(l => MapSummary(l, categories, templates)).ToList(),
            @new       = listings.Where(l => !l.IsFeatured).Take(6)
                                 .Select(l => MapSummary(l, categories, templates)).ToList(),
            city
        });
    }

    [HttpGet("/home/explore")]
    public async Task<IActionResult> Explore(
        [FromQuery] string? city,
        [FromQuery] string? category,
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
        var templates  = await LoadTemplatesAsync(rows, ct);

        var items = rows.Select(l => MapSummary(l, categories, templates)).ToList();
        return this.OkEnvelope("home.explore", items);
    }

    [HttpGet("/home/search/suggestions")]
    public IActionResult Suggestions() =>
        this.OkEnvelope("home.search.suggestions", new
        {
            recent  = Array.Empty<string>(),
            popular = new[] { "إب", "صنعاء", "عدن", "جديد", "مستعمل" }
        });

    [HttpGet("/legal")]
    public IActionResult Legal() =>
        this.OkEnvelope("legal.list", new[]
        {
            new { key = "terms",   label = "الشروط والأحكام" },
            new { key = "privacy", label = "سياسة الخصوصية"  },
            new { key = "refund",  label = "سياسة الاسترداد" },
        });

    // ── helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// يُحَمِّل قَوالِب كُلّ الفِئات المُستَخدَمَة في القائِمَة دَفعَة واحِدَة
    /// (لِتَجَنُّب N+1). DB أَوَّلاً، fallback لِلكود لِكُلّ slug لا row لَه.
    /// يُعيد bundle بِـ CategoryId→Slug map + Slug→Template map.
    /// </summary>
    private async Task<TemplateBundle> LoadTemplatesAsync(
        IReadOnlyList<ProductListingEntity> listings, CancellationToken ct)
    {
        var bundle = new TemplateBundle();
        var categoryIds = listings.Where(l => l.CategoryId.HasValue)
                                  .Select(l => l.CategoryId!.Value).Distinct().ToList();
        if (categoryIds.Count == 0) return bundle;

        // ① مَصدَر الإنتاج. لِكُلّ id نَطلُب template — مَقبول لِأَنّ عَدَد
        //    الفِئات في صَفحَة home/explore صَغير (تَقريباً عَدَد الفِئات
        //    المُختَلِفَة بَين 60 إعلان كَأَقصى حَدّ).
        bundle.IdToSlug = await _db.ProductCategories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Slug })
            .ToDictionaryAsync(c => c.Id, c => c.Slug, ct);

        foreach (var id in categoryIds)
        {
            var fromProd = await _prodSource.BuildForCategoryAsync(id, ct);
            if (fromProd is { Fields.Count: > 0 })
                bundle.IdToTemplate[id] = fromProd;
        }

        if (bundle.IdToSlug.Count == 0) return bundle;

        // ② DB-served seed لِما لَم يُغَطِّه الإنتاج.
        var slugsNeedingFallback = bundle.IdToSlug
            .Where(kv => !bundle.IdToTemplate.ContainsKey(kv.Key))
            .Select(kv => kv.Value).Distinct().ToList();
        var dbRows = slugsNeedingFallback.Count == 0
            ? new Dictionary<string, string>()
            : await _db.CategoryAttributeTemplates.AsNoTracking()
                .Where(t => slugsNeedingFallback.Contains(t.CategorySlug))
                .ToDictionaryAsync(t => t.CategorySlug, t => t.TemplateJson, ct);

        foreach (var kv in bundle.IdToSlug)
        {
            if (bundle.IdToTemplate.ContainsKey(kv.Key)) continue;
            AttributeTemplate? template = null;
            if (dbRows.TryGetValue(kv.Value, out var json))
                template = DynamicAttributeHelper.ParseTemplate(json);
            // لا fallback لِكود — كُلّ تَسميات السِمات تَأتي مِن DB فَقَط.
            if (template is not null) bundle.IdToTemplate[kv.Key] = template;
        }
        return bundle;
    }

    private sealed class TemplateBundle
    {
        public Dictionary<Guid, string> IdToSlug { get; set; } = new();
        /// <summary>قالَب مُجَهَّز لِكُلّ Category.Id (أَيّ مَصدَر).</summary>
        public Dictionary<Guid, AttributeTemplate> IdToTemplate { get; } = new();
    }

    private static object MapSummary(
        ProductListingEntity l,
        IReadOnlyList<ACommerce.Kits.Discovery.Domain.DiscoveryCategory> categories,
        TemplateBundle templates)
    {
        var attributes = BuildAttributes(l, templates);
        return new
        {
            id           = l.Id,
            title        = l.Title,
            price        = l.Price,
            timeUnit     = "fixed",
            timeUnitLabel= "",
            propertyType = l.Condition ?? "",
            propertyTypeLabel = categories.FirstOrDefault(c => c.Slug == l.Condition)?.Label ?? l.Condition ?? "",
            city         = l.City    ?? "",
            district     = l.Address ?? "",
            lat          = l.Latitude ?? 0,
            lng          = l.Longitude ?? 0,
            bedroomCount = 0,
            areaSqm      = 0,
            isVerified   = l.IsFeatured,
            viewsCount   = l.ViewCount,
            isFavorite   = false,
            amenities    = Array.Empty<string>(),
            firstImage   = l.FeaturedImage ?? FirstFromJson(l.ImagesJson),
            attributes   = attributes,
        };
    }

    private static List<DynamicAttribute> BuildAttributes(ProductListingEntity l, TemplateBundle templates)
    {
        var raw = ParseLegacy(l.AttributesJson);
        if (raw.Count == 0) return new();

        AttributeTemplate? template = null;
        if (l.CategoryId is { } cid && templates.IdToTemplate.TryGetValue(cid, out var t))
            template = t;

        if (template is null || template.Fields.Count == 0)
            return RawSnapshot(raw);

        return DynamicAttributeHelper.BuildSnapshot(template, raw);
    }

    private static Dictionary<string, object?> ParseLegacy(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return new();
            var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in doc.RootElement.EnumerateObject())
                d[p.Name] = Extract(p.Value);
            return d;
        }
        catch { return new(); }
    }

    private static object? Extract(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var i) ? i : (object)el.GetDouble(),
        JsonValueKind.True   => true,
        JsonValueKind.False  => false,
        JsonValueKind.Null   => null,
        JsonValueKind.Array  => el.EnumerateArray().Select(Extract).ToList(),
        JsonValueKind.Object => el.TryGetProperty("value", out var v) ? Extract(v) : el.GetRawText(),
        _ => null,
    };

    private static List<DynamicAttribute> RawSnapshot(Dictionary<string, object?> raw)
    {
        var i = 0;
        return raw.Where(kv => kv.Value is not null).Select(kv => new DynamicAttribute
        {
            Key = kv.Key, Label = kv.Key, LabelAr = kv.Key,
            Type = kv.Value is bool ? "bool" : (kv.Value is long or double ? "number" : "text"),
            Value = kv.Value, SortOrder = ++i,
        }).ToList();
    }

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
