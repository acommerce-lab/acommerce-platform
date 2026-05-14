namespace ACommerce.Kits.Taxonomy.Operations;

/// <summary>
/// عُقدَة شَجَرَة تَصنيف. كُلّ نِظام شَجَرَة في التَطبيق يَتَكَوَّن مَن
/// عُقَد بِنَفس الشَكل، يُمَيِّزها <see cref="RootCode"/>. النَموذَج
/// <b>مُتَعَدِّد الجُذور</b>: جَدول واحِد يَحوي شَجَرَة "فِئات الإعلانات"
/// + شَجَرَة "المُدُن" + شَجَرَة "الاِهتِمامات" بِلا تَعارُض.
///
/// <para>الـ <see cref="Code"/> فَريد ضِمن <c>(RootCode, ParentId)</c> —
/// أَيّ "apartment" في root "real_estate" مَع ParentId=residential تَستَطيع
/// التَكرار في شَجَرَة أُخرى لكِنّ الـ slug العالَمي يُفَضَّل أَن يَكون
/// فَريداً ضِمن root واحِد لِتَسهيل الاستِعلام عَلى Listings.PropertyType.</para>
/// </summary>
public interface ITaxonomyNode
{
    Guid Id { get; }
    Guid? ParentId { get; }

    /// <summary>مُعَرِّف الشَجَرَة (<c>"listing_categories"</c>،
    /// <c>"locations"</c>، …). يَفصِل الأَشجار في جَدول واحِد.</summary>
    string RootCode { get; }

    /// <summary>slug فَريد ضِمن الشَجَرَة. يُستَخدَم كَ key في الـ Listing
    /// (<c>IListing.PropertyType = node.Code</c>) — هذا هو الجِسر بَين
    /// الكيتَين بِلا coupling.</summary>
    string Code { get; }

    string  Name   { get; }
    string? NameAr { get; }

    /// <summary>اسم أَيقونَة (bootstrap-icons، lucide، …) بِلا بادِئَة.</summary>
    string? Icon { get; }

    int  SortOrder { get; }
    bool IsActive  { get; }
}

/// <summary>POCO قابِل لِلتَسلسُل (الـ controller يُرجِعه عَبر envelope).</summary>
public sealed record TaxonomyNodeView(
    Guid    Id,
    Guid?   ParentId,
    string  RootCode,
    string  Code,
    string  Name,
    string? NameAr,
    string? Icon,
    int     SortOrder,
    bool    IsActive
) : ITaxonomyNode;
