namespace AshareMigrator.Legacy;

public class LegacyUser
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string PasswordHash { get; set; } = "";
    public bool EmailVerified { get; set; }
    public bool PhoneNumberVerified { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

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
    public decimal? CompareAtPrice { get; set; }
    public int QuantityAvailable { get; set; }
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsNew { get; set; }
    public int ViewCount { get; set; }
    public decimal? Rating { get; set; }
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
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal? QuarterlyPrice { get; set; }
    public decimal? SemiAnnualPrice { get; set; }
    public decimal? AnnualPrice { get; set; }
    public int TrialDays { get; set; }
    public int GracePeriodDays { get; set; }
    public int MaxListings { get; set; }
    public int MaxImagesPerListing { get; set; }
    public int MaxFeaturedListings { get; set; }
    public int StorageLimitMB { get; set; }
    public int CommissionType { get; set; }
    public decimal CommissionPercentage { get; set; }
    public bool HasVerifiedBadge { get; set; }
}

public class LegacySubscription
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid VendorId { get; set; }
    public Guid PlanId { get; set; }
    public int Status { get; set; }
    public int BillingCycle { get; set; }
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
    public Guid UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PreferredLanguage { get; set; }
}
