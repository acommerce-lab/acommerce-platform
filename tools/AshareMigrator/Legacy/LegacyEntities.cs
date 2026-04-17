namespace AshareMigrator.Legacy;

// لا يوجد جدول Users في المصدر — UserId مخزّن كنص في Profile

public class LegacyVendor
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid ProfileId { get; set; }
    public string StoreName { get; set; } = "";
    public string StoreSlug { get; set; } = "";
    public string? Description { get; set; }
    public string? Logo { get; set; }
    public int Status { get; set; }
    public int CommissionType { get; set; }
    public decimal CommissionValue { get; set; }
    public decimal? Rating { get; set; }
    public int TotalSales { get; set; }
}

public class LegacyCategory
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string? Image { get; set; }
    public string? Icon { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class LegacyListing
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid VendorId { get; set; }
    public Guid? CategoryId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }
    public int ViewCount { get; set; }
    public string? ImagesJson { get; set; }
    public string? FeaturedImage { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Currency { get; set; }
    public string? AttributesJson { get; set; }
    public int Status { get; set; }
}

public class LegacyBooking
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid SpaceId { get; set; }
    public string CustomerId { get; set; } = "";
    public Guid HostId { get; set; }
    public string? SpaceName { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal DepositAmount { get; set; }
    public string Currency { get; set; } = "SAR";
    public int Status { get; set; }
    public string? CustomerNotes { get; set; }
    public string? HostNotes { get; set; }
    public int GuestsCount { get; set; }
}

public class LegacySubscriptionPlan
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; } = "";
    public string? NameEn { get; set; }
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public bool IsRecommended { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal? QuarterlyPrice { get; set; }
    public decimal? SemiAnnualPrice { get; set; }
    public decimal? AnnualPrice { get; set; }
    public string Currency { get; set; } = "SAR";
    public int TrialDays { get; set; }
    public int GracePeriodDays { get; set; }
    public int MaxListings { get; set; }
    public int MaxImagesPerListing { get; set; }
    public int MaxFeaturedListings { get; set; }
    public int StorageLimitMB { get; set; }
    public int MaxTeamMembers { get; set; }
    public int MaxMonthlyMessages { get; set; }
    public int MaxMonthlyApiCalls { get; set; }
    public int ListingDurationDays { get; set; }
    public string CommissionType { get; set; } = "percentage";
    public decimal CommissionPercentage { get; set; }
    public decimal CommissionFixedAmount { get; set; }
    public decimal? MinCommission { get; set; }
    public decimal? MaxCommission { get; set; }
    public bool HasVerifiedBadge { get; set; }
    public int SearchPriorityBoost { get; set; }
    public string AnalyticsLevel { get; set; } = "basic";
    public string SupportLevel { get; set; } = "standard";
    public bool AllowDirectMessages { get; set; }
    public bool AllowApiAccess { get; set; }
    public bool AllowCustomStorePage { get; set; }
    public bool AllowPromotionalTools { get; set; }
    public bool AllowDataExport { get; set; }
    public bool RemoveBranding { get; set; }
    public bool EmailReports { get; set; }
    public bool PushNotifications { get; set; }
}

public class LegacySubscription
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid VendorId { get; set; }
    public Guid PlanId { get; set; }
    public string Status { get; set; } = "";
    public string BillingCycle { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "SAR";
    public int MaxListings { get; set; }
    public int CurrentListingsCount { get; set; }
    public bool AutoRenew { get; set; }
}

public class LegacyProfile
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public string? UserId { get; set; }
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Avatar { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; }
}
