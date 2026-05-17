using ACommerce.Compositions.Customer.Marketplace.Home.Backend;
using ACommerce.Kits.Discovery.Domain;
using ACommerce.Kits.Listings.Domain;
using ACommerce.SharedKernel.Domain.DynamicAttributes;
using Ashare.V3.Data;
using Ashare.V3.Data.Templates;
using Ashare.V3.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ashare.V3.Api.Home;

/// <summary>
/// تَنفيذ <see cref="IHomeListingsSource"/> فَوق <see cref="AshareV3DbContext"/>.
/// عشير V3 يَستَعمِل <c>IsActive</c> بَدَل <c>Status==1</c> في إيجار، وَ
/// <c>Condition</c> كَ slug فِئَة بَدَل <c>PropertyType</c> (تَفصيل asharedb).
/// </summary>
public sealed class AshareV3HomeListingsSource : IHomeListingsSource
{
    private readonly AshareV3DbContext _db;
    public AshareV3HomeListingsSource(AshareV3DbContext db) => _db = db;

    public async Task<IReadOnlyList<IListing>> GetActiveListingsAsync(string? city, CancellationToken ct)
    {
        var rows = await _db.ProductListings.AsNoTracking()
            .Where(l => l.IsActive && (string.IsNullOrEmpty(city) || l.City == city))
            .ToListAsync(ct);
        return rows.Cast<IListing>().ToList();
    }

    public async Task<IReadOnlyList<IListing>> ExploreAsync(ExploreFilter f, CancellationToken ct)
    {
        var query = _db.ProductListings.AsNoTracking().Where(l => l.IsActive);
        if (!string.IsNullOrWhiteSpace(f.City))         query = query.Where(l => l.City == f.City);
        if (!string.IsNullOrWhiteSpace(f.PropertyType)) query = query.Where(l => l.Condition == f.PropertyType);
        if (f.MinPrice is { } minP)                     query = query.Where(l => l.Price >= minP);
        if (f.MaxPrice is { } maxP)                     query = query.Where(l => l.Price <= maxP);
        if (!string.IsNullOrWhiteSpace(f.Query))
        {
            var s = f.Query.Trim();
            query = query.Where(l =>
                l.Title.Contains(s) ||
                (l.Description != null && l.Description.Contains(s)) ||
                (l.City != null && l.City.Contains(s)) ||
                (l.Address != null && l.Address.Contains(s)));
        }
        query = f.Sort switch
        {
            "newest"     => query.OrderByDescending(l => l.CreatedAt),
            "price_asc"  => query.OrderBy(l => l.Price),
            "price_desc" => query.OrderByDescending(l => l.Price),
            _            => query.OrderByDescending(l => l.IsFeatured).ThenByDescending(l => l.CreatedAt),
        };

        var rows = await query.Take(60).ToListAsync(ct);
        return rows.Cast<IListing>().ToList();
    }
}

/// <summary>
/// تَنفيذ <see cref="IHomeListingProjection"/> لِـ V3 — يُضيف
/// <c>attributes</c> (DynamicAttribute snapshot) لِكُلّ بِطاقَة.
///
/// <para>الـ template يُحَمَّل دَفعَة واحِدَة عَبر
/// <see cref="LoadAndCache"/> الَّذي يُكاش (Category.Id → Template) خِلال
/// مُدَّة سُكوب الـ scoped (طَلَب HTTP واحِد). يَتَجَنَّب N+1.</para>
/// </summary>
public sealed class AshareV3HomeListingProjection : IHomeListingProjection
{
    private readonly AshareV3DbContext _db;
    private readonly ProductionAttributeTemplateSource _prodSource;
    private readonly Dictionary<Guid, AttributeTemplate?> _templateCache = new();
    private bool _slugCacheLoaded;
    private Dictionary<Guid, string> _idToSlug = new();
    private Dictionary<string, string> _slugToJsonSeed = new();

    public AshareV3HomeListingProjection(
        AshareV3DbContext db,
        ProductionAttributeTemplateSource prodSource)
    {
        _db = db;
        _prodSource = prodSource;
    }

    public object MapCard(IListing l, IReadOnlyList<DiscoveryCategory> categories)
    {
        // الكَيان الـ EF الأَصلي مَوصول كَ IListing فَقَط. نَستَردّ
        // الـ ProductListingEntity مُباشَرَةً عَبر تَحقيق الـ cast — كُلّ
        // الـ source يَنتُج ProductListingEntity فَقَط.
        var entity = (ProductListingEntity)l;
        var attributes = BuildAttributes(entity);

        return new
        {
            id                = l.Id,
            title             = l.Title,
            price             = l.Price,
            timeUnit          = "fixed",
            timeUnitLabel     = "",
            propertyType      = entity.Condition ?? "",
            propertyTypeLabel = categories.FirstOrDefault(c => c.Slug == entity.Condition)?.Label
                                ?? entity.Condition ?? "",
            city              = l.City,
            district          = entity.Address ?? "",
            lat               = entity.Latitude ?? 0,
            lng               = entity.Longitude ?? 0,
            bedroomCount      = 0,
            areaSqm           = 0,
            isVerified        = entity.IsFeatured,
            viewsCount        = entity.ViewCount,
            isFavorite        = false,
            amenities         = Array.Empty<string>(),
            firstImage        = entity.FeaturedImage ?? FirstFromJson(entity.ImagesJson),
            attributes,
        };
    }

    private List<DynamicAttribute> BuildAttributes(ProductListingEntity l)
    {
        var raw = ParseLegacy(l.AttributesJson);
        if (raw.Count == 0) return new();

        var template = l.CategoryId is { } cid ? GetTemplate(cid) : null;
        return template is { Fields.Count: > 0 }
            ? DynamicAttributeHelper.BuildSnapshot(template, raw)
            : RawSnapshot(raw);
    }

    private AttributeTemplate? GetTemplate(Guid categoryId)
    {
        if (_templateCache.TryGetValue(categoryId, out var cached)) return cached;

        // مَصدَر إنتاج أَوَّلاً (نَفس مَنطِق HomeController القَديم).
        var fromProd = _prodSource.BuildForCategoryAsync(categoryId, CancellationToken.None)
            .GetAwaiter().GetResult();
        if (fromProd is { Fields.Count: > 0 })
        {
            _templateCache[categoryId] = fromProd;
            return fromProd;
        }

        // fallback: CategoryAttributeTemplates seed (DB JSON).
        EnsureSlugCache();
        if (_idToSlug.TryGetValue(categoryId, out var slug) &&
            _slugToJsonSeed.TryGetValue(slug, out var json))
        {
            var parsed = DynamicAttributeHelper.ParseTemplate(json);
            _templateCache[categoryId] = parsed;
            return parsed;
        }
        _templateCache[categoryId] = null;
        return null;
    }

    private void EnsureSlugCache()
    {
        if (_slugCacheLoaded) return;
        _slugCacheLoaded = true;
        _idToSlug = _db.ProductCategories.AsNoTracking()
            .Select(c => new { c.Id, c.Slug })
            .ToDictionary(c => c.Id, c => c.Slug);
        _slugToJsonSeed = _db.CategoryAttributeTemplates.AsNoTracking()
            .ToDictionary(t => t.CategorySlug, t => t.TemplateJson);
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

/// <summary>اقتِراحات بَحث سُعودِيَّة لِـ V3.</summary>
public sealed class AshareV3HomeSearchSuggestions : IHomeSearchSuggestions
{
    public IReadOnlyList<string> Popular { get; } =
        new[] { "الرياض", "جدة", "الدمام", "مكة المكرمة", "المدينة المنورة" };
    public IReadOnlyList<string> Recent { get; } = Array.Empty<string>();
}

/// <summary>Discovery categories مَن جَدول V3 <c>DiscoveryCategories</c>.</summary>
public sealed class AshareV3DiscoveryCategoryProvider : IDiscoveryCategoryProvider
{
    private readonly AshareV3DbContext _db;
    public AshareV3DiscoveryCategoryProvider(AshareV3DbContext db) => _db = db;

    public async Task<IReadOnlyList<DiscoveryCategory>> GetCategoriesAsync(CancellationToken ct)
        => await _db.DiscoveryCategories.AsNoTracking().ToListAsync(ct);
}
