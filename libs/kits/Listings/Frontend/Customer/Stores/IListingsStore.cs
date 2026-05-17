using ACommerce.Kits.Listings.Domain;

namespace ACommerce.Kits.Listings.Frontend.Customer.Stores;

/// <summary>
/// عقد الـ store الـ reactive للإعلانات على الـ client. التطبيق يحقّقه ضدّ
/// <c>IClientOpEngine</c> + state محلّيّ، ويُسجِّله عبر
/// <c>AddDomainBindings(b =&gt; b.Use&lt;IListingsStore, EjarListingsStore&gt;())</c>.
///
/// <para>صفحات الكيت لا تَرى إلا هذا الـ interface — لا EF، لا
/// <c>HttpClient</c>، لا app entities. الواجهة تُعطي بيانات بـ
/// <see cref="IListing"/> فقط (Law 6).</para>
/// </summary>
public interface IListingsStore
{
    /// <summary>الإعلانات المرئيّة بعد آخر فلتر/بحث.</summary>
    IReadOnlyList<IListing> Visible { get; }

    /// <summary>إعلانات المستخدِم الحاليّ.</summary>
    IReadOnlyList<IListing> Mine { get; }

    /// <summary>true بينما يَجلب الـ store دفعة جديدة.</summary>
    bool IsLoading { get; }

    /// <summary>الفلتر الحاليّ — يُمرَّر للسيرفر مع <see cref="ApplyFilterAsync"/>.</summary>
    ListingFilter Filter { get; }

    /// <summary>يُطلَق عند أيّ تغيير حالة — للـ <c>StateHasChanged</c>.</summary>
    event Action? Changed;

    /// <summary>يَجلب صفحة جديدة من <see cref="Visible"/> بناءً على فلتر.</summary>
    Task ApplyFilterAsync(ListingFilter filter, CancellationToken ct = default);

    /// <summary>يَجلب إعلاناً واحداً بـ id (يُحدِّث كاش داخليّ).</summary>
    Task<IListing?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>يَجلب قائمة الـ "إعلاناتي" للمستخدِم الحاليّ.</summary>
    Task LoadMineAsync(CancellationToken ct = default);

    /// <summary>
    /// يَنشُر إعلاناً جَديداً. التَطبيق يَبني <see cref="ListingDraftPayload"/>
    /// مِن مَصدَر مَحَلّيّ (مَثَل <c>IListingDraft</c>) ويُمَرِّره. يَنجح ⇒
    /// يُضيف الإعلان لِـ <see cref="Mine"/> ويُطلِق Changed.
    /// </summary>
    Task<IListing?> CreateAsync(ListingDraftPayload payload, CancellationToken ct = default);

    /// <summary>يُحَدِّث إعلاناً قائِماً (PATCH). جَميع حُقول
    /// <see cref="ListingDraftPayload"/> اختِيارِيَّة عَبر الـ kit
    /// (الـ store يَتَجاهَل القِيَم الافتِراضِيَّة الَّتي لَم تَتَغَيَّر —
    /// التَطبيق يُمَرِّر القِيَم الكامِلَة).</summary>
    Task<IListing?> UpdateAsync(string id, ListingDraftPayload payload, CancellationToken ct = default);

    /// <summary>يُبَدِّل حالة إعلان (نَشِط ↔ مُتَوَقِّف). يُحَدِّث <see cref="Mine"/>.</summary>
    Task ToggleStatusAsync(string id, CancellationToken ct = default);

    /// <summary>يَحذِف إعلاناً يَملِكه المُستَخدِم. يُزيله مِن <see cref="Mine"/>.</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);
}

/// <summary>
/// حُمولة إنشاء إعلان — DTO مَكشوفَة عَلى مُستوى الـ kit. تَطبيقات تَبنيها
/// مِن واجِهَتِها (مَثَل <c>IListingDraft</c>) وتُمَرِّرها للـ Store. كلّ
/// الحقول مُطابِقَة لِـ <c>ListingsController.CreateBody</c>.
/// </summary>
public sealed record ListingDraftPayload(
    string  Title,
    string? Description,
    decimal Price,
    string  TimeUnit,
    string? PropertyType,
    string  City,
    string? District,
    double? Lat,
    double? Lng,
    int     BedroomCount,
    int     BathroomCount,
    int     AreaSqm,
    IReadOnlyList<string> Amenities,
    IReadOnlyList<string> Images,
    string? Thumbnail,
    /// <summary>سِمات ديناميكِيَّة (مَفاتيح القالَب → قِيَم). يُرسَل
    /// كَ <c>attributes</c> في الـ body. <c>null</c> إن لَم تَجمَع
    /// الواجِهَة قَيماً (تَطبيقات بِلا قَوالِب فِئات).</summary>
    IReadOnlyDictionary<string, object?>? Attributes = null,
    /// <summary>مِفتاح حِمايَة مَن التَّكرار — يُرسَل كَـ <c>idempotency_key</c>
    /// tag عَلى الـ operation. <b>الواجِهَة تُوَلِّده مَرَّة واحِدَة لِكُلّ
    /// "نِيَّة كِتابَة"</b> (مَثَل: عَلى أَوَّل ضَغطَة Publish) وتُعيد
    /// استِخدامه عِندَ إعادَة المُحاوَلَة. <c>null</c> ⇒ السيرفر يُوَلِّد
    /// مِفتاحاً (لا dedup فِعليّ).</summary>
    Guid? IdempotencyKey = null);

/// <summary>فلتر بحث/استكشاف للإعلانات. POCO قابل للـ serialize.</summary>
public sealed record ListingFilter(
    string? City = null,
    string? PropertyType = null,
    decimal? PriceMin = null,
    decimal? PriceMax = null,
    int? BedroomsMin = null,
    string? Query = null,
    int Page = 1,
    int PageSize = 20)
{
    public static ListingFilter Empty { get; } = new();
}
