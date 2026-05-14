namespace ACommerce.Compositions.Customer.Marketplace.Home;

/// <summary>
/// store reactive لِتَجميعات الـ marketplace home عَلى العَميل. تَلفّ
/// endpoints الـ backend الخاصّة بِالـ marketplace التي لا تَنتَمي لِكيت
/// واحِد (تَجميع categories + featured + new + explore + suggestions + legal).
///
/// <para>كلّ نِداء يَخرُج كَ OAM op عَبر <c>ITemplateEngine</c> فيَستَفيد
/// مِن interceptors المُسَجَّلة (culture localization، telemetry، retry).
/// لا HttpClient مُباشَر — هذه الـ composition pure-OAM.</para>
///
/// <para>صَفحات Home/Explore/Search تَستَهلِك هذا الـ interface فَقَط.</para>
/// </summary>
public interface IMarketplaceHomeStore
{
    /// <summary>آخِر <c>HomeView</c> مَجلوب — null قَبل أَوَّل <see cref="LoadHomeAsync"/>.</summary>
    HomeView? Home { get; }

    /// <summary>نَتائج آخِر <see cref="ApplyExploreAsync"/> (شَكل V1 المُسَطَّح).</summary>
    IReadOnlyList<HomeListingCard> Explore { get; }

    /// <summary>عُناوين الوَثائق القانونيّة — null قَبل أَوّل <see cref="LoadLegalAsync"/>.</summary>
    IReadOnlyList<LegalDoc>? LegalDocs { get; }

    /// <summary>اقتِراحات بَحث — null قَبل أَوَل <see cref="LoadSuggestionsAsync"/>.</summary>
    SearchSuggestions? Suggestions { get; }

    bool IsLoading { get; }
    event Action? Changed;

    /// <summary>يَجلب <c>/home/view?city=…</c>: categories + featured + @new.</summary>
    Task LoadHomeAsync(string? city = null, CancellationToken ct = default);

    /// <summary>يَجلب <c>/home/explore?…</c>: قائمة مُسَطَّحَة بِالحُقول الكامِلة.</summary>
    Task ApplyExploreAsync(ExploreFilter filter, CancellationToken ct = default);

    /// <summary>يَجلب <c>/home/search/suggestions</c>: recent + popular.</summary>
    Task LoadSuggestionsAsync(CancellationToken ct = default);

    /// <summary>يَجلب <c>/legal</c>: قائمة (key, label) مِن الوَثائق المُتاحة.</summary>
    Task LoadLegalAsync(CancellationToken ct = default);
}

/// <summary>
/// شَكل ردّ <c>/home/view</c>: تَجميعَة categories + featured + @new + city.
/// </summary>
public sealed record HomeView(
    IReadOnlyList<HomeCategoryItem>      Categories,
    IReadOnlyList<HomeListingCard>       Featured,
    IReadOnlyList<HomeListingCard>       New,
    string?                              City);

/// <summary>
/// شَكل عُنصُر إعلان مُسَطَّح كما يَردّه <c>/home/view</c> + <c>/home/explore</c>.
/// يَحوي الحُقول التي تَحتاجها بِطاقَة العَرض مُباشَرَةً (label + thumb +
/// metadata) دون mapping إضافيّ.
/// </summary>
public sealed record HomeListingCard(
    string  Id,
    string  Title,
    decimal Price,
    string? TimeUnit,
    string? TimeUnitLabel,
    string? PropertyType,
    string? PropertyTypeLabel,
    string? City,
    string? District,
    double? Lat,
    double? Lng,
    int     BedroomCount,
    int     AreaSqm,
    bool    IsVerified,
    int     ViewsCount,
    bool    IsFavorite,
    IReadOnlyList<string> Amenities,
    string? FirstImage,
    // snapshots سِمات ديناميكِيَّة (Template+Snapshot). الـ enricher في
    // التَطبيق يَملَؤها لِكُلّ إعلان. null/empty ⇒ البِطاقَة لا تَعرِض chips.
    IReadOnlyList<ACommerce.SharedKernel.Domain.DynamicAttributes.DynamicAttribute>? Attributes = null);

/// <summary>عُنصُر فِئَة كما يَأتي مَع <c>/home/view</c> (مُختَصَر — slug + label + icon).</summary>
public sealed record HomeCategoryItem(string Id, string Label, string? Icon);

/// <summary>وَثيقَة قانونيّة (key + label) — يَدخُل لِـ /legal/{key} لِلمَتن.</summary>
public sealed record LegalDoc(string Key, string Label);

/// <summary>اقتِراحات بَحث: سَجَلّ + شائِع.</summary>
public sealed record SearchSuggestions(
    IReadOnlyList<string> Recent,
    IReadOnlyList<string> Popular);

/// <summary>
/// فلتر <c>/home/explore</c>. مَطابِق لِـ <c>HomeController.Explore</c> queryparams:
/// city, propertyType (alias = category), q, minBedrooms, minPrice, maxPrice, sort.
/// </summary>
public sealed record ExploreFilter(
    string?  City         = null,
    string?  PropertyType = null,
    string?  Query        = null,
    int      MinBedrooms  = 0,
    decimal? MinPrice     = null,
    decimal? MaxPrice     = null,
    string?  Sort         = null)
{
    public static ExploreFilter Empty { get; } = new();
}
