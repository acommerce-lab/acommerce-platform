using AshareMigrator.Legacy;
using AshareMigrator.Target;

namespace AshareMigrator.Mappers;

public static class PlanMapper
{
    /// <summary>
    /// يحوّل LegacySubscriptionPlan → NewPlan.
    /// المخطط القديم يطابق الجديد تقريباً — نسخ مباشر.
    /// </summary>
    public static NewPlan Map(LegacySubscriptionPlan src) => new()
    {
        Id = src.Id,
        CreatedAt = DateTime.SpecifyKind(src.CreatedAt, DateTimeKind.Utc),
        UpdatedAt = src.UpdatedAt,
        IsDeleted = false,
        Name = src.Name,
        NameEn = src.NameEn,
        Slug = src.Slug,
        Description = src.Description,
        DescriptionEn = src.DescriptionEn,
        Icon = src.Icon,
        Color = src.Color,
        SortOrder = src.SortOrder,
        IsActive = src.IsActive,
        IsDefault = src.IsDefault,
        IsRecommended = src.IsRecommended,
        MonthlyPrice = src.MonthlyPrice,
        QuarterlyPrice = src.QuarterlyPrice,
        SemiAnnualPrice = src.SemiAnnualPrice,
        AnnualPrice = src.AnnualPrice,
        Currency = src.Currency,
        TrialDays = src.TrialDays,
        GracePeriodDays = src.GracePeriodDays > 0 ? src.GracePeriodDays : 3,
        MaxListings = src.MaxListings,
        MaxImagesPerListing = src.MaxImagesPerListing > 0 ? src.MaxImagesPerListing : 5,
        MaxFeaturedListings = src.MaxFeaturedListings,
        StorageLimitMB = src.StorageLimitMB > 0 ? src.StorageLimitMB : 500,
        MaxTeamMembers = src.MaxTeamMembers,
        MaxMonthlyMessages = src.MaxMonthlyMessages,
        MaxMonthlyApiCalls = src.MaxMonthlyApiCalls,
        ListingDurationDays = src.ListingDurationDays,
        CommissionType = src.CommissionType,
        CommissionPercentage = src.CommissionPercentage,
        CommissionFixedAmount = src.CommissionFixedAmount,
        MinCommission = src.MinCommission,
        MaxCommission = src.MaxCommission,
        HasVerifiedBadge = src.HasVerifiedBadge,
        SearchPriorityBoost = src.SearchPriorityBoost,
        AnalyticsLevel = src.AnalyticsLevel,
        SupportLevel = src.SupportLevel,
        AllowDirectMessages = src.AllowDirectMessages,
        AllowApiAccess = src.AllowApiAccess,
        AllowCustomStorePage = src.AllowCustomStorePage,
        AllowPromotionalTools = src.AllowPromotionalTools,
        AllowDataExport = src.AllowDataExport,
        RemoveBranding = src.RemoveBranding,
        EmailReports = src.EmailReports,
        PushNotifications = src.PushNotifications,
    };
}

public static class SubscriptionMapper
{
    /// <summary>
    /// يحوّل LegacySubscription → NewSubscription.
    /// Status و BillingCycle نصوص في المصدر (nvarchar) — تُمرَّر كما هي.
    /// </summary>
    public static NewSubscription Map(LegacySubscription src, Guid userId) => new()
    {
        Id = src.Id,
        CreatedAt = DateTime.SpecifyKind(src.CreatedAt, DateTimeKind.Utc),
        UpdatedAt = src.UpdatedAt,
        IsDeleted = false,
        UserId = userId,
        PlanId = src.PlanId,
        BillingCycle = src.BillingCycle,
        StartDate = DateTime.SpecifyKind(src.StartDate, DateTimeKind.Utc),
        EndDate = DateTime.SpecifyKind(src.CurrentPeriodEnd, DateTimeKind.Utc),
        TrialEndDate = src.TrialEndDate.HasValue
            ? DateTime.SpecifyKind(src.TrialEndDate.Value, DateTimeKind.Utc)
            : null,
        Status = MapStatus(src.Status),
        AmountPaid = src.Price,
        Currency = string.IsNullOrWhiteSpace(src.Currency) ? "SAR" : src.Currency,
        UsedListingsCount = src.CurrentListingsCount,
        LastUsageReset = DateTime.SpecifyKind(src.StartDate, DateTimeKind.Utc),
    };

    private static int MapStatus(string status) => status.ToLowerInvariant() switch
    {
        "active" => 1,
        "trial" or "trialing" => 1,
        "past_due" or "pastdue" => 2,
        "cancelled" or "canceled" => 3,
        "expired" => 4,
        "suspended" => 5,
        _ => 0
    };
}
