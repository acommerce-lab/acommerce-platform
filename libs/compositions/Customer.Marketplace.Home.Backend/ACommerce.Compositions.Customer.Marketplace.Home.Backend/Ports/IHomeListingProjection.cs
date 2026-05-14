using ACommerce.Kits.Discovery.Domain;
using ACommerce.Kits.Listings.Domain;

namespace ACommerce.Compositions.Customer.Marketplace.Home.Backend;

/// <summary>
/// مَنفَذ يُحَوِّل <see cref="IListing"/> إلى wire DTO (anonymous object)
/// لِبِطاقَة <c>AcMarketplaceHomePage</c>.
///
/// <para><b>لِماذا projection مُنفَصِلَة</b>: شَكل البِطاقَة يَختَلِف بَين
/// التَطبيقات:
/// <list type="bullet">
///   <item>إيجار: <c>timeUnit</c> + <c>timeUnitLabel</c> ("شَهرِيّاً") + <c>firstImage</c>
///         مَن <c>ImagesCsv</c>.</item>
///   <item>عشير V3: يُضيف <c>attributes</c> (DynamicAttribute snapshot)
///         مَبني مَن قالَب فِئَة الإعلان + <c>AttributesJson</c>.</item>
/// </list></para>
///
/// <para>الـ <see cref="DefaultHomeListingProjection"/> يَخدِم البِنيَة
/// الأَدنى الَّتي يَتَوَقَّعها <c>AcSpaceCard</c> بِناءً عَلى حُقول <c>IListing</c>
/// فَقَط. التَطبيق يُسَجِّل impl خاصَّاً يَفوز عَلى الـ default إذا احتاج إضافات.</para>
///
/// <para><b>الفَرق عَن <c>IListingDetailEnricher</c></b>: ذاك لِصَفحَة
/// <c>/listings/{id}</c> (تَفاصيل واحِدَة)، هذا لِلبِطاقات في صَفحَة
/// Home/Explore (٦٠ بِطاقَة دَفعَة واحِدَة، N+1-aware).</para>
/// </summary>
public interface IHomeListingProjection
{
    /// <summary>
    /// يَبني wire DTO مَن إعلان. الـ <paramref name="categories"/> مُمَرَّر
    /// مَرَّة واحِدَة لِلصَفحَة كَكُلّ (لا N+1 على lookups).
    /// </summary>
    object MapCard(IListing listing, IReadOnlyList<DiscoveryCategory> categories);
}

/// <summary>
/// تَنفيذ افتِراضي يَستَخدِم حُقول <see cref="IListing"/> فَقَط — كافٍ
/// لِتَطبيقات بِلا سِمات ديناميكِيَّة. التَطبيقات الَّتي تَحتاج labels مُتَرجَمَة
/// أَو snapshot attrs تُسَجِّل impl خاصَّاً يَفوز.
/// </summary>
public sealed class DefaultHomeListingProjection : IHomeListingProjection
{
    public object MapCard(IListing l, IReadOnlyList<DiscoveryCategory> categories) => new
    {
        id                = l.Id,
        title             = l.Title,
        price             = l.Price,
        timeUnit          = l.TimeUnit,
        timeUnitLabel     = l.TimeUnit, // التَطبيق يَستَطيع override لِتَرجَمَة
        propertyType      = l.PropertyType,
        propertyTypeLabel = categories.FirstOrDefault(c => c.Slug == l.PropertyType)?.Label
                            ?? l.PropertyType,
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
