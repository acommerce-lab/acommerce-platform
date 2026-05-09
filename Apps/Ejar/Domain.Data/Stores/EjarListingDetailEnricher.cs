using ACommerce.Kits.Discovery.Domain;
using ACommerce.Kits.Listings.Backend;
using ACommerce.Kits.Listings.Domain;
using Ejar.Api.Data;
using Ejar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Stores;

/// <summary>
/// يُغني <see cref="IListing"/> بحقول إضافيّة تتوقّعها واجهة إيجار:
/// <list type="bullet">
///   <item><c>TimeUnitLabel</c> + <c>PropertyTypeLabel</c> من Discovery.</item>
///   <item><c>Owner</c> (Id/Name/MemberSince) من Users.</item>
///   <item><c>Amenities</c> كقائمة <c>{ key, label }</c> بدل سلاسل خامّ.</item>
/// </list>
/// قبل هذا الـ enricher كانت صفحة تفاصيل الإعلان تنكسر بعد M3 لأنّ
/// <c>ListingsController.Get</c> كان يُرجع <c>IListing</c> فقط (slugs بلا labels،
/// بلا owner)، بينما <c>ListingDetailDto</c> في الواجهة يطلب الـ labels والـ owner.
/// </summary>
public sealed class EjarListingDetailEnricher : IListingDetailEnricher
{
    private readonly EjarDbContext _db;
    public EjarListingDetailEnricher(EjarDbContext db) => _db = db;

    public async Task<object> EnrichAsync(IListing l, CancellationToken ct)
    {
        var categories = await _db.DiscoveryCategories.AsNoTracking().ToListAsync(ct);
        var amenityRows = l.Amenities.Count == 0
            ? new List<DiscoveryAmenity>()
            : await _db.DiscoveryAmenities.AsNoTracking()
                .Where(a => l.Amenities.Contains(a.Slug)).ToListAsync(ct);

        UserEntity? owner = null;
        if (Guid.TryParse(l.OwnerId, out var oid))
            owner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == oid, ct);

        return new
        {
            id           = l.Id,
            title        = l.Title,
            description  = l.Description,
            price        = l.Price,
            timeUnit     = l.TimeUnit,
            timeUnitLabel = l.TimeUnit switch
            {
                "daily"   => "يوم",
                "monthly" => "شهر",
                "yearly"  => "سنة",
                "weekly"  => "أسبوع",
                _         => l.TimeUnit
            },
            propertyType      = l.PropertyType,
            propertyTypeLabel = categories.FirstOrDefault(c => c.Slug == l.PropertyType)?.Label ?? l.PropertyType,
            city          = l.City,
            district      = l.District,
            lat           = l.Lat,
            lng           = l.Lng,
            bedroomCount  = l.BedroomCount,
            bathroomCount = l.BathroomCount,
            areaSqm       = l.AreaSqm,
            isVerified    = l.IsVerified,
            viewsCount    = l.ViewsCount,
            status        = l.Status,
            isFavorite    = false,    // الواجهة تتولّى ذلك من Store.FavoriteListingIds
            images        = l.Images,
            amenities     = amenityRows.Select(a => new { key = a.Slug, label = a.Label }).ToList(),
            ownerId       = l.OwnerId,
            owner         = owner is null ? null : new
            {
                id          = owner.Id.ToString(),
                name        = owner.FullName,
                memberSince = owner.MemberSince.ToString("yyyy-MM-dd")
            }
        };
    }
}
