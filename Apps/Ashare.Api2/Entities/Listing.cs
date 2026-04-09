using ACommerce.SharedKernel.Abstractions.Entities;

namespace Ashare.Api2.Entities;

/// <summary>
/// عرض/إعلان في عشير. يحل محل Catalog.Products في النسخة السابقة.
/// كل الحقول صريحة - لا أصناف ديناميكية.
/// </summary>
public class Listing : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid OwnerId { get; set; }
    public Guid CategoryId { get; set; }

    // === الحقول العامة ===
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal Price { get; set; }
    public int Duration { get; set; }
    public string TimeUnit { get; set; } = "month";   // "day", "week", "month", "year"
    public string Currency { get; set; } = "SAR";

    // === الموقع ===
    public string City { get; set; } = default!;
    public string? District { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }

    // === تفاصيل العقار (سكني/تجاري/إداري) ===
    public string? PropertyType { get; set; }   // "villa", "apartment", "office"...
    public string? UnitType { get; set; }       // "studio", "room"...
    public int? Floor { get; set; }
    public double? Area { get; set; }
    public int? Rooms { get; set; }
    public int? Bathrooms { get; set; }
    public bool? Furnished { get; set; }
    public string? Amenities { get; set; }      // CSV: "wifi,ac,elevator"

    // === طلبات شريك السكن ===
    public string? PersonalName { get; set; }
    public int? Age { get; set; }
    public string? Gender { get; set; }
    public string? Nationality { get; set; }
    public string? Job { get; set; }
    public bool? Smoking { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    // === تفضيلات التواصل ===
    public bool IsPhoneAllowed { get; set; } = true;
    public bool IsWhatsAppAllowed { get; set; } = true;
    public bool IsMessagingAllowed { get; set; } = true;

    // === الترخيص ===
    public string? LicenseNumber { get; set; }

    // === الصور (CSV من URLs) ===
    public string? ImagesCsv { get; set; }

    // === الحالة ===
    public ListingStatus Status { get; set; } = ListingStatus.Draft;
    public DateTime? PublishedAt { get; set; }
    public int ViewCount { get; set; }
    public bool IsFeatured { get; set; }

    // === ربط محاسبي بالاشتراك (FIFO consumption) ===
    /// <summary>الاشتراك الذي استُهلكت منه حصة هذا العرض</summary>
    public Guid? SubscriptionId { get; set; }
    /// <summary>لقطة من معرّف الباقة وقت الإنشاء (لئلا يتأثر بتغيير لاحق)</summary>
    public Guid? PlanIdSnapshot { get; set; }
    /// <summary>بداية ونهاية فترة الاشتراك التي يخصها</summary>
    public DateTime? BillingPeriodStart { get; set; }
    public DateTime? BillingPeriodEnd { get; set; }
    /// <summary>معرّف العملية المحاسبية لإنشاء العرض</summary>
    public Guid? OperationId { get; set; }
}

public enum ListingStatus
{
    Draft = 0,
    Published = 1,
    Reserved = 2,
    Closed = 3,
    Rejected = 4
}
