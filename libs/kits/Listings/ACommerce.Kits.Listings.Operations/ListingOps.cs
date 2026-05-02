using ACommerce.OperationEngine.Core;

namespace ACommerce.Kits.Listings.Operations;

/// <summary>أنواع عمليّات Listings — typed.</summary>
public static class ListingOps
{
    public static readonly OperationType Create = new("listing.create");
    public static readonly OperationType Edit   = new("listing.edit");
    public static readonly OperationType Toggle = new("listing.toggle");
    public static readonly OperationType Delete = new("listing.delete");
    public static readonly OperationType View   = new("listing.view");
}

public static class ListingTagKeys
{
    public static readonly TagKey OwnerId      = new("listing_owner_id");
    public static readonly TagKey PropertyType = new("listing_property_type");
    public static readonly TagKey City         = new("listing_city");
    public static readonly TagKey District     = new("listing_district");
    public static readonly TagKey Status       = new("listing_status");
}

public static class ListingMarkers
{
    public static readonly Marker IsListing =
        new(new TagKey("kind"), new TagValue("listing"));
}

/// <summary>
/// فلتر بحث الإعلانات. الكيت يُمرِّره للـ store؛ تطبيقات قد تستهلكه عبر
/// CrudActionInterceptor لو فضّلت الـ generic CRUD path. كلّ الحقول
/// اختياريّة — null/0 = "لا فلتر على هذا الحقل".
/// </summary>
public sealed record ListingFilter(
    string?  City              = null,
    string?  District          = null,
    string?  PropertyType      = null,
    string?  TimeUnit          = null,
    decimal? PriceMin          = null,
    decimal? PriceMax          = null,
    string?  Search            = null,
    int      MinBedrooms       = 0,
    int      MinAreaSqm        = 0,
    bool     OnlyVerified      = false,
    string?  Sort              = null,    // "newest" | "price_asc" | "price_desc" | default = "popular"
    int      Page              = 1,
    int      PageSize          = 20);
