namespace ACommerce.Kits.Discovery.Frontend.Customer.Stores;

/// <summary>
/// store reactive لِكاتالوج الـ Discovery kit عَلى الـ client. يَكشِف
/// المُدُن وَ الفِئات وَ وَسائِل الراحَة المُسَجَّلَة في السيرفر — تَطبيقات
/// marketplace تَستَهلِكها لِبِناء pickers وَ filter bars وَ home aggregations.
///
/// <para>الكاتالوج read-only مِن جِهَة العَميل (الإدارة فَقَط تَكتُب). الـ
/// <see cref="LoadAsync"/> يَجلب القَوائم الثَلاث في طَلَب واحِد —
/// تَطبيقات تُريد lazy load كلّ منها مُنفَرِداً تُسَجِّل تَنفيذاً مُخَصَّصاً.</para>
/// </summary>
public interface IDiscoveryStore
{
    /// <summary>أَسماء المُدُن (Level=1 في DiscoveryRegion).</summary>
    IReadOnlyList<string> Cities { get; }

    /// <summary>وَسائِل الراحَة (slug + label) — لِـ filter chips + amenity grid.</summary>
    IReadOnlyList<DiscoveryAmenityItem> Amenities { get; }

    /// <summary>الفِئات (slug + label + icon + kind) — لِـ category picker.</summary>
    IReadOnlyList<DiscoveryCategoryItem> Categories { get; }

    bool IsLoading { get; }
    event Action? Changed;

    /// <summary>يَجلب القَوائم الثَلاث (cities + amenities + categories) بِالتَوازي.</summary>
    Task LoadAsync(CancellationToken ct = default);

    /// <summary>يَجلب المُدُن فَقَط (لِصَفحات لا تَحتاج الباقي).</summary>
    Task LoadCitiesAsync(CancellationToken ct = default);

    /// <summary>يَجلب وَسائِل الراحَة فَقَط.</summary>
    Task LoadAmenitiesAsync(CancellationToken ct = default);

    /// <summary>يَجلب الفِئات فَقَط.</summary>
    Task LoadCategoriesAsync(CancellationToken ct = default);
}

/// <summary>عُنصر وَسيلَة راحَة. مُحاذٍ لـ <c>GET /amenities</c>: <c>{ key, label }</c>.</summary>
public sealed record DiscoveryAmenityItem(string Key, string Label);

/// <summary>عُنصر فِئَة. مُحاذٍ لـ <c>GET /categories</c>: <c>{ id, label, icon, kind }</c>.</summary>
public sealed record DiscoveryCategoryItem(string Id, string Label, string Icon, string Kind);
