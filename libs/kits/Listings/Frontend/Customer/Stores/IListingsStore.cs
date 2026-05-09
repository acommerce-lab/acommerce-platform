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
}

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
