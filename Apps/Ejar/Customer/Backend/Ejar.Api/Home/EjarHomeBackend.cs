using ACommerce.Compositions.Customer.Marketplace.Home.Backend;
using ACommerce.Kits.Discovery.Domain;
using ACommerce.Kits.Listings.Domain;
using ACommerce.Kits.Taxonomy.Backend;
using Ejar.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Home;

/// <summary>
/// تَنفيذ <see cref="IHomeListingsSource"/> فَوق <see cref="EjarDbContext"/>.
/// <c>Status==1</c> = نَشِط في إيجار. الفلتر يَدعَم city/propertyType/q/price/
/// bedrooms. اختيار <c>PropertyType</c> = slug في شَجَرَة Taxonomy ⇒
/// نُحَوِّله إلى subtree slugs (kind ⇒ كُلّ leaves تَحته) فَيَفلتِر الإعلانات
/// بِأَيّ طَبَقَة، لا leaves فَقَط.
/// </summary>
public sealed class EjarHomeListingsSource : IHomeListingsSource
{
    private readonly EjarDbContext  _db;
    private readonly ITaxonomyStore _taxonomy;
    public EjarHomeListingsSource(EjarDbContext db, ITaxonomyStore taxonomy)
    {
        _db = db;
        _taxonomy = taxonomy;
    }

    public async Task<IReadOnlyList<IListing>> GetActiveListingsAsync(string? city, CancellationToken ct)
    {
        var rows = await _db.Listings.AsNoTracking()
            .Where(l => l.Status == 1 && (string.IsNullOrEmpty(city) || l.City == city))
            .ToListAsync(ct);
        return rows.Cast<IListing>().ToList();
    }

    public async Task<IReadOnlyList<IListing>> ExploreAsync(ExploreFilter f, CancellationToken ct)
    {
        var query = _db.Listings.AsNoTracking().Where(l => l.Status == 1);
        if (!string.IsNullOrWhiteSpace(f.City)) query = query.Where(l => l.City == f.City);
        if (!string.IsNullOrWhiteSpace(f.PropertyType))
        {
            // subtree: اختيار "residential" ⇒ apartment+villa+studio+room+...
            // اختيار "apartment" ⇒ apartment فَقَط (الـ slug نَفسه دائِماً
            // ضِمن نَتيجَة GetSubtreeSlugsAsync).
            var slugs = await _taxonomy.GetSubtreeSlugsAsync("listing_categories", f.PropertyType, ct);
            var list = slugs.Count > 0 ? (IReadOnlyList<string>)slugs : new[] { f.PropertyType };
            query = query.Where(l => list.Contains(l.PropertyType));
        }
        if (f.MinPrice is { } minP)                     query = query.Where(l => l.Price >= minP);
        if (f.MaxPrice is { } maxP)                     query = query.Where(l => l.Price <= maxP);
        if (f.MinBedrooms > 0)                          query = query.Where(l => l.BedroomCount >= f.MinBedrooms);
        if (!string.IsNullOrWhiteSpace(f.Query))
        {
            var s = f.Query.Trim();
            query = query.Where(l =>
                l.Title.Contains(s) || l.Description.Contains(s) ||
                l.City.Contains(s)  || l.District.Contains(s));
        }
        query = f.Sort switch
        {
            "newest"     => query.OrderByDescending(l => l.CreatedAt),
            "price_asc"  => query.OrderBy(l => l.Price),
            "price_desc" => query.OrderByDescending(l => l.Price),
            _            => query.OrderByDescending(l => l.IsVerified).ThenByDescending(l => l.CreatedAt),
        };

        var rows = await query.Take(60).ToListAsync(ct);
        return rows.Cast<IListing>().ToList();
    }
}

/// <summary>
/// تَنفيذ <see cref="IHomeListingProjection"/> لِإيجار. يُضيف
/// <c>timeUnitLabel</c> مُتَرجَم + <c>firstImage</c> مَن <c>ImagesCsv</c>
/// كَ fallback لِلإعلانات القَديمَة بِلا <c>ThumbnailUrl</c>.
/// </summary>
public sealed class EjarHomeListingProjection : IHomeListingProjection
{
    public object MapCard(IListing l, IReadOnlyList<DiscoveryCategory> categories) => new
    {
        id                = l.Id,
        title             = l.Title,
        price             = l.Price,
        timeUnit          = l.TimeUnit,
        timeUnitLabel     = l.TimeUnit switch
        {
            "monthly" => "شهرياً",
            "yearly"  => "سنوياً",
            "daily"   => "يومياً",
            _ => l.TimeUnit,
        },
        propertyType      = l.PropertyType,
        propertyTypeLabel = categories.FirstOrDefault(c => c.Slug == l.PropertyType)?.Label ?? l.PropertyType,
        city              = l.City,
        district          = l.District,
        lat               = l.Lat,
        lng               = l.Lng,
        bedroomCount      = l.BedroomCount,
        areaSqm           = l.AreaSqm,
        isVerified        = l.IsVerified,
        viewsCount        = l.ViewsCount,
        isFavorite        = false,
        amenities         = l.Amenities,
        firstImage        = l.ThumbnailUrl ?? (l.Images.Count > 0 ? l.Images[0] : null),
    };
}

/// <summary>اقتِراحات بَحث يَمَنِيَّة لِإيجار.</summary>
public sealed class EjarHomeSearchSuggestions : IHomeSearchSuggestions
{
    public IReadOnlyList<string> Popular { get; } =
        new[] { "إب", "فيلا", "شقة مفروشة", "مكتب", "استراحة" };
    public IReadOnlyList<string> Recent { get; } = Array.Empty<string>();
}

/// <summary>Discovery categories مَن جَدول إيجار <c>DiscoveryCategories</c>.</summary>
public sealed class EjarDiscoveryCategoryProvider : IDiscoveryCategoryProvider
{
    private readonly EjarDbContext _db;
    public EjarDiscoveryCategoryProvider(EjarDbContext db) => _db = db;

    public async Task<IReadOnlyList<DiscoveryCategory>> GetCategoriesAsync(CancellationToken ct)
        => await _db.DiscoveryCategories.AsNoTracking().ToListAsync(ct);
}
