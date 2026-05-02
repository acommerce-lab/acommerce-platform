namespace ACommerce.Kits.Listings.Domain;

/// <summary>
/// عقد الإعلان الأدنى الذي يستهلكه Listings kit. كيان الـ DB في التطبيق
/// يُلصِقه (Law 6 في <c>CLAUDE.md</c>): الكيت يتعامل مع interface فقط، الـ App
/// يحتفظ بشكل التخزين الذي يناسبه + يضيف ما يشاء (custom fields، metadata).
///
/// <para>الـ ١٤ خاصّيّة هنا تكفي لكلّ مسارات Listings.Backend الافتراضيّة:
/// قائمة + بحث + فلتر + تفاصيل + إنشاء + تعديل + تبديل حالة + حذف.</para>
/// </summary>
public interface IListing
{
    string  Id { get; }
    string  OwnerId { get; }
    string  Title { get; }
    string  Description { get; }
    decimal Price { get; }
    /// <summary>"daily" | "monthly" | "yearly" | … (محايد عن المدّة).</summary>
    string  TimeUnit { get; }
    /// <summary>"apartment" | "villa" | … — يطابق Discovery categories slugs.</summary>
    string  PropertyType { get; }
    string  City { get; }
    string  District { get; }
    double  Lat { get; }
    double  Lng { get; }
    int     BedroomCount { get; }
    int     BathroomCount { get; }
    int     AreaSqm { get; }
    /// <summary>1 = active، 2 = paused. التطبيق يضيف حالات أخرى لو احتاج.</summary>
    int     Status { get; }
    /// <summary>عداد المشاهدة — للفرز والإحصاء.</summary>
    int     ViewsCount { get; }
    bool    IsVerified { get; }
    /// <summary>thumbnail base64 صغير (~30KB) للبطاقات. null لو لا صور.</summary>
    string? ThumbnailUrl { get; }
    /// <summary>قائمة صور كاملة (URLs أو base64) — pipe-separated في كثير من التطبيقات.</summary>
    IReadOnlyList<string> Images { get; }
    /// <summary>وسائل الراحة (slugs من Discovery.Amenities).</summary>
    IReadOnlyList<string> Amenities { get; }
    DateTime CreatedAt { get; }
    DateTime? UpdatedAt { get; }
}

/// <summary>
/// تطبيق POCO نقيّ لـ <see cref="IListing"/> — لا EF، لا DB. ListingsController
/// يبنيه عند POST/PATCH ويضعه على <c>ctx.WithEntity&lt;IListing&gt;()</c>،
/// فينساب لكلّ post-interceptor (notify-watchers، mark-trending، …) مستقلّاً
/// عن persistence.
/// </summary>
public sealed record InMemoryListing(
    string  Id,
    string  OwnerId,
    string  Title,
    string  Description,
    decimal Price,
    string  TimeUnit,
    string  PropertyType,
    string  City,
    string  District,
    double  Lat,
    double  Lng,
    int     BedroomCount,
    int     BathroomCount,
    int     AreaSqm,
    int     Status,
    int     ViewsCount,
    bool    IsVerified,
    string? ThumbnailUrl,
    IReadOnlyList<string> Images,
    IReadOnlyList<string> Amenities,
    DateTime CreatedAt,
    DateTime? UpdatedAt = null
) : IListing;
