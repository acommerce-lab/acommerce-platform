using ACommerce.Kits.Listings.Domain;

namespace ACommerce.Compositions.Customer.Marketplace.Home.Backend;

/// <summary>
/// مَنفَذ يَجلِب الإعلانات لِصَفحَة Home/Explore. التَطبيق يُنَفِّذه فَوق
/// DbContext الخاصّ بِه — الـ composition لا تَعلَم بِأَيّ EF شَكل.
///
/// <para>المُهِمَّتان:
/// <list type="number">
///   <item><see cref="GetActiveListingsAsync"/> — كُلّ المَنشورَة (الإعلان
///         <c>Status==active</c>)، اختياريّاً مُقَيَّدَة بِـ city. تُستَهلَك
///         عَبر <c>/home/view</c>: المُكَوِّن يَفصِل featured/new.</item>
///   <item><see cref="ExploreAsync"/> — يَطبَّق فلتر <see cref="ExploreFilter"/>
///         الكامِل (city/type/q/min-max/bedrooms/sort). تَطبيقات تَختار شَكل
///         الـ SQL: WhereThenContains، Full-Text، إلخ.</item>
/// </list></para>
///
/// <para>المُخرَج <see cref="IReadOnlyList{T}"/> مَن <c>IListing</c> — أَيّ
/// كَيان EF يُنَفِّذ الواجِهَة يَنتَقِل بِلا تَحويل. الـ Projection يَأخُذ
/// المُخرَج ويُنتِج wire DTO.</para>
/// </summary>
public interface IHomeListingsSource
{
    /// <summary>كُلّ الإعلانات النَّشِطَة، اختياريّاً مُقَيَّدَة بِـ city.
    /// تَرتيب الـ implementation حُرّ — الـ controller يَختار الـ featured/new
    /// بِنَفسه بَعد ذلك.</summary>
    Task<IReadOnlyList<IListing>> GetActiveListingsAsync(string? city, CancellationToken ct);

    /// <summary>قائِمَة Explore بَعد تَطبيق فلتر كامِل + sort. الـ controller
    /// يَتَوَقَّع أَنّ النَّتائج جاهِزَة لِلعَرض (max ~60 صَفّاً عادَةً).</summary>
    Task<IReadOnlyList<IListing>> ExploreAsync(ExploreFilter filter, CancellationToken ct);
}

/// <summary>فلتر Explore مُوَحَّد عَبر التَطبيقات. أَيّ حَقل null = لا تَطبيق.</summary>
public sealed record ExploreFilter(
    string? City,
    /// <summary>slug لِفِئَة الإعلان (<c>apartment</c>, <c>roommate_has</c>، …).
    /// يُطابِق <c>IListing.PropertyType</c>.</summary>
    string? PropertyType,
    /// <summary>نَصّ بَحث حُرّ — التَطبيق يَختار الأَعمِدَة الَّتي يَفحَصها
    /// (title/description/city/district، إلخ).</summary>
    string? Query,
    decimal? MinPrice,
    decimal? MaxPrice,
    int MinBedrooms,
    /// <summary><c>newest</c> | <c>price_asc</c> | <c>price_desc</c> | <c>null</c>
    /// (افتِراضي = الـ featured أَوَّلاً + الأَحدَث).</summary>
    string? Sort);
