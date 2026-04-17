namespace AshareMigrator.Target;

public class NewUser
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public string PhoneNumber { get; set; } = "";
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? NationalId { get; set; }
    public bool NafathVerified { get; set; }
    public bool IsActive { get; set; } = true;
    public string Role { get; set; } = "customer";
}

public class NewCategory
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public string Slug { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string? AttributeTemplateJson { get; set; }
}

public class NewListing
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid OwnerId { get; set; }
    public Guid CategoryId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public int Duration { get; set; } = 1;
    public string TimeUnit { get; set; } = "month";
    public string Currency { get; set; } = "SAR";
    public string City { get; set; } = "";
    public string? District { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public bool IsPhoneAllowed { get; set; } = true;
    public bool IsWhatsAppAllowed { get; set; } = true;
    public bool IsMessagingAllowed { get; set; } = true;
    public string? LicenseNumber { get; set; }
    public string? ImagesCsv { get; set; }
    public string? DynamicAttributesJson { get; set; }
    public int Status { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int ViewCount { get; set; }
    public bool IsFeatured { get; set; }
    public Guid? SubscriptionId { get; set; }
    public Guid? PlanIdSnapshot { get; set; }
    public DateTime? BillingPeriodStart { get; set; }
    public DateTime? BillingPeriodEnd { get; set; }
    public Guid? OperationId { get; set; }
}

public class NewBooking
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid ListingId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid OwnerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "SAR";
    public int Status { get; set; }
    public string? Notes { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? OperationId { get; set; }
}

public class NewPlan
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
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public bool IsRecommended { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal? QuarterlyPrice { get; set; }
    public decimal? SemiAnnualPrice { get; set; }
    public decimal? AnnualPrice { get; set; }
    public string Currency { get; set; } = "SAR";
    public int TrialDays { get; set; }
    public int GracePeriodDays { get; set; } = 3;
    public int MaxListings { get; set; }
    public int MaxImagesPerListing { get; set; } = 5;
    public int MaxFeaturedListings { get; set; }
    public int StorageLimitMB { get; set; } = 500;
    public int MaxTeamMembers { get; set; } = 1;
    public int MaxMonthlyMessages { get; set; } = -1;
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
    public bool AllowDirectMessages { get; set; } = true;
    public bool AllowApiAccess { get; set; }
    public bool AllowCustomStorePage { get; set; }
    public bool AllowPromotionalTools { get; set; }
    public bool AllowDataExport { get; set; }
    public bool RemoveBranding { get; set; }
    public bool EmailReports { get; set; }
    public bool PushNotifications { get; set; } = true;
    public string? AllowedCategorySlugs { get; set; }
}

public class NewSubscription
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public string BillingCycle { get; set; } = "monthly";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public int Status { get; set; }
    public decimal AmountPaid { get; set; }
    public string Currency { get; set; } = "SAR";
    public Guid? PaymentId { get; set; }
    public int UsedListingsCount { get; set; }
    public int UsedFeaturedListingsCount { get; set; }
    public int UsedMonthlyMessages { get; set; }
    public int UsedMonthlyApiCalls { get; set; }
    public DateTime LastUsageReset { get; set; }
    public Guid? OperationId { get; set; }
}

public class NewProfile
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? CoverUrl { get; set; }
    public string? Bio { get; set; }
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string PreferredLanguage { get; set; } = "ar";
    public bool IsPhonePublic { get; set; }
    public bool IsEmailPublic { get; set; }
    public int ListingsCount { get; set; }
    public double? Rating { get; set; }
    public int RatingCount { get; set; }
}
