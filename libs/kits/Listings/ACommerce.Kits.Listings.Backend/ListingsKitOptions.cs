namespace ACommerce.Kits.Listings.Backend;

public sealed class ListingsKitOptions
{
    /// <summary>PartyKind في tags الـ OAM (لأطراف العمليّة). افتراضيّ "User".</summary>
    public string PartyKind { get; set; } = "User";

    /// <summary>الحدّ الأقصى لطول العنوان.</summary>
    public int MaxTitleLength { get; set; } = 200;

    /// <summary>الحدّ الأقصى لطول الوصف.</summary>
    public int MaxDescriptionLength { get; set; } = 2000;

    /// <summary>أكبر pageSize مسموح في GET /listings (حماية من DoS بـ paging كبير).</summary>
    public int MaxPageSize { get; set; } = 60;
}
