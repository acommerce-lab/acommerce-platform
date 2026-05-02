using ACommerce.Kits.Listings.Domain;
using ACommerce.Kits.Listings.Operations;

namespace ACommerce.Kits.Listings.Backend;

/// <summary>
/// عقد تخزين الإعلانات. التطبيق ينفّذه. الـ store يحفظ الـ DB-entity ولا
/// يَفرض على Listings kit أيّ شكل — الكيت يتعامل مع <see cref="IListing"/>
/// فقط (Law 6).
///
/// <para>كلّ الـ writes <c>NoSaveAsync</c> (F6 + H3): يُسجِّل tracked على
/// DbContext دون <c>SaveChangesAsync</c>؛ الـ controller يضع
/// <c>.SaveAtEnd()</c> على القيد فيُحفَظ ذرّيّاً مع أيّ tracked entity
/// أُضيف داخل المعترضات (notify-watchers، audit، …).</para>
/// </summary>
public interface IListingStore
{
    // ── reads ────────────────────────────────────────────────────────────
    /// <summary>قائمة الإعلانات النشطة + فلترة + ترتيب + paging.</summary>
    Task<IReadOnlyList<IListing>> SearchAsync(ListingFilter filter, CancellationToken ct);

    /// <summary>عدد الكلّيّ المطابق للفلتر — لـ paging.</summary>
    Task<int> CountAsync(ListingFilter filter, CancellationToken ct);

    /// <summary>إعلان واحد. <c>null</c> لو غير موجود/محذوف.</summary>
    Task<IListing?> GetAsync(string id, CancellationToken ct);

    /// <summary>قائمة إعلانات هذا المستخدم — للوحته الشخصيّة.</summary>
    Task<IReadOnlyList<IListing>> ListByOwnerAsync(string ownerId, CancellationToken ct);

    // ── writes (F6: tracker-only) ────────────────────────────────────────
    /// <summary>أضف إعلاناً جديداً للـ ChangeTracker. لا save.</summary>
    Task AddNoSaveAsync(IListing listing, CancellationToken ct);

    /// <summary>طبّق التعديلات على إعلان موجود (PATCH semantics).
    /// null على حقل = "أبقِ القديم". لا save.</summary>
    Task<bool> UpdateNoSaveAsync(string id, ListingUpdate patch, CancellationToken ct);

    /// <summary>بدّل بين active (1) و paused (2). لا save.</summary>
    Task<int?> ToggleStatusNoSaveAsync(string id, CancellationToken ct);

    /// <summary>soft-delete. لا save.</summary>
    Task<bool> DeleteNoSaveAsync(string id, CancellationToken ct);

    /// <summary>تأكيد ملكيّة — مطلوب قبل أيّ تعديل/حذف.</summary>
    Task<bool> IsOwnerAsync(string id, string ownerId, CancellationToken ct);

    /// <summary>زِد عدّاد المشاهدة بـ 1 (atomic). لا save.</summary>
    Task IncrementViewCountNoSaveAsync(string id, CancellationToken ct);
}

/// <summary>
/// رزمة تعديل الإعلان (PATCH). كلّ حقل null = "لا تغيِّر". الـ store يُطبِّق
/// فقط الـ non-null.
/// </summary>
public sealed record ListingUpdate(
    string?  Title,
    string?  Description,
    decimal? Price,
    string?  TimeUnit,
    string?  PropertyType,
    string?  City,
    string?  District,
    double?  Lat,
    double?  Lng,
    int?     BedroomCount,
    int?     BathroomCount,
    int?     AreaSqm,
    IReadOnlyList<string>? Amenities,
    IReadOnlyList<string>? Images,
    string? Thumbnail);
