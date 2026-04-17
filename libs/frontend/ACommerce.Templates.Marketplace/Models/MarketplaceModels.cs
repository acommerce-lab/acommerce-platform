namespace ACommerce.Templates.Marketplace.Models;

// ── DTOs لمنصة السوق العقاري ──────────────────────────────────────────────
// يتبع نفس مبدأ SharedModels: حقول محددة + Extra bag للتوسع.

/// <summary>
/// صف إعلان في قوائم/بطاقات الإعلانات.
/// </summary>
public sealed record ListingRowDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required decimal Price { get; init; }
    public string Currency { get; init; } = "SAR";
    public string? TimeUnit { get; init; }  // "month", "day", "night", "week"
    public string? City { get; init; }
    public string? District { get; init; }
    public string? CategoryName { get; init; }
    public int Status { get; init; }  // 0=draft,1=published,2=reserved,3=closed,4=rejected
    public bool IsFeatured { get; init; }
    public int ViewCount { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? OwnerName { get; init; }
    public string? OwnerAvatarUrl { get; init; }
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>
/// صف حجز في قوائم/بطاقات الحجوزات.
/// </summary>
public sealed record BookingRowDto
{
    public required string Id { get; init; }
    public required decimal TotalPrice { get; init; }
    public string Currency { get; init; } = "SAR";
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int Status { get; init; }  // 0=pending,1=confirmed,2=awaitingPayment,3=paid,4=cancelled,5=completed
    public string? Notes { get; init; }
    public string? ListingTitle { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public bool Acting { get; set; }  // optimistic UI: disable buttons while processing
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>
/// نموذج إنشاء/تعديل إعلان — يُستخدم في AcListingForm.
/// </summary>
public sealed record ListingFormModel
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string TimeUnit { get; set; } = "month";
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Address { get; set; }
    public string? CategoryId { get; set; }
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public double? Area { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// معلومات الاشتراك للوحة التحكم/الإدارة.
/// </summary>
public sealed record SubscriptionInfoDto
{
    public required string PlanName { get; init; }
    public string? PlanDescription { get; init; }
    public required decimal MonthlyPrice { get; init; }
    public string Currency { get; init; } = "SAR";
    public required int Status { get; init; }  // 0=trial,1=active,2=cancelled,3=expired
    public DateTime? ExpiresAt { get; init; }
    public int ListingQuota { get; init; }
    public int ListingUsed { get; init; }
    public int FeaturedQuota { get; init; }
    public int FeaturedUsed { get; init; }
    public string? SubscriptionId { get; init; }
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>
/// بطاقة إحصاء في لوحة تحكم المالك.
/// </summary>
public sealed record OwnerStatDto
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public string? IconName { get; init; }
    public string? Trend { get; init; }   // "up" | "down" | null
    public string? TrendValue { get; init; }
    public Dictionary<string, object?>? Extra { get; init; }
}

/// <summary>
/// صف خطة اشتراك في صفحة الإدارة.
/// </summary>
public sealed record PlanRowDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required decimal MonthlyPrice { get; init; }
    public string Currency { get; init; } = "SAR";
    public string Slug { get; init; } = "";
}

/// <summary>
/// فئة في الصفحة الرئيسية (صف أفقي من AcCategoryTile).
/// </summary>
public sealed record CategoryTileDto
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public string Icon { get; init; } = "tag";
}

/// <summary>
/// حمولة الصفحة الرئيسية: فئات + مميّزة + جديدة.
/// تُقرأ بواسطة ApiReader، ثم تُمرّر إلى AcMarketplaceHomePage.
/// </summary>
public sealed record MarketplaceHomeDto
{
    public IReadOnlyList<CategoryTileDto> Categories { get; init; } = Array.Empty<CategoryTileDto>();
    public IReadOnlyList<ListingRowDto> Featured { get; init; } = Array.Empty<ListingRowDto>();
    public IReadOnlyList<ListingRowDto> New { get; init; } = Array.Empty<ListingRowDto>();
}
