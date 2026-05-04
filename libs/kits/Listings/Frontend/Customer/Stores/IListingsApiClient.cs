using ACommerce.Kits.Listings.Domain;

namespace ACommerce.Kits.Listings.Frontend.Customer.Stores;

/// <summary>
/// عميل HTTP خاصّ بـ Listings kit. يَعرف <b>تَماماً</b> شكل الـ envelopes
/// التي تَردّها <c>ListingsController</c> ويَقَشِّرها للـ widgets/stores.
/// التطبيق لا يُكَرِّر هذه المعرفة في bindings — يُسَجِّل تنفيذاً واحداً
/// (أو يَقبل الافتراضيّ <c>HttpListingsApiClient</c>) ويَنتهي.
///
/// <para>القاعدة: كلّ kit يَعرف شكل ردّه — لا يَجوز للتطبيق أن يُحاول
/// تَخمين الشكل في bindings. أيّ تَغيير في الـ controller (إضافة pagination
/// مثلاً) يُعالَج هنا في kit واحد بدلاً من التطبيقات.</para>
/// </summary>
public interface IListingsApiClient
{
    /// <summary>GET /listings — قائمة paginated. يَرجع IListing[] + total.</summary>
    Task<ListingPageResult> SearchAsync(ListingFilter filter, CancellationToken ct = default);

    /// <summary>GET /listings/{id} — تَفاصيل إعلان واحد.</summary>
    Task<IListing?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>GET /my-listings — إعلانات المستخدم الحاليّ (مُصادَق).</summary>
    Task<IReadOnlyList<IListing>> ListMineAsync(CancellationToken ct = default);
}

/// <summary>صفحة نَتائج بحث.</summary>
public sealed record ListingPageResult(
    IReadOnlyList<IListing> Items,
    int Total,
    int Page,
    int PageSize);
