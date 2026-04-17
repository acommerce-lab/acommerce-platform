using ACommerce.SharedKernel.Abstractions.Entities;

namespace Ashare.Api.Entities;

/// <summary>
/// عرض/إعلان في عشير. الحقول الخاصة بالفئات (نوع العقار، الغرف، الأمنيات، إلخ)
/// تُخزَّن كقائمة لقطات في <see cref="DynamicAttributesJson"/> وفق قالب الفئة الذي يحدّده Category.
/// تغيير قالب الفئة لاحقاً لا يعدّل الإعلانات القديمة (Snapshot pattern).
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

    // === تفضيلات التواصل ===
    public bool IsPhoneAllowed { get; set; } = true;
    public bool IsWhatsAppAllowed { get; set; } = true;
    public bool IsMessagingAllowed { get; set; } = true;

    // === الترخيص ===
    public string? LicenseNumber { get; set; }

    // === الصور (CSV من URLs) ===
    public string? ImagesCsv { get; set; }

    // === لقطة سمات ديناميكية (List<DynamicAttribute> JSON) ===
    /// <summary>
    /// لقطة كاملة لقيم القالب المُختار من الفئة وقت الإنشاء/التعديل.
    /// لا تحلّ — يُستهلك مباشرة بـ DynamicAttributeHelper.ParseAttributes.
    /// </summary>
    public string? DynamicAttributesJson { get; set; }

    // === الحالة ===
    public ListingStatus Status { get; set; } = ListingStatus.Draft;
    public DateTime? PublishedAt { get; set; }
    public int ViewCount { get; set; }
    public bool IsFeatured { get; set; }

    // === ربط محاسبي بالاشتراك (FIFO consumption) ===
    public Guid? SubscriptionId { get; set; }
    public Guid? PlanIdSnapshot { get; set; }
    public DateTime? BillingPeriodStart { get; set; }
    public DateTime? BillingPeriodEnd { get; set; }
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
