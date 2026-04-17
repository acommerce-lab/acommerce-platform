using AshareMigrator.Legacy;
using AshareMigrator.Target;

namespace AshareMigrator.Mappers;

public static class PlanMapper
{
    /// <summary>
    /// يحوّل LegacySubscriptionPlan → NewPlan.
    /// CommissionType القديم enum (int) → وصف نصي ("percentage"/"fixed"/"tiered").
    /// الحقول الجديدة غير الموجودة في القديم (MaxTeamMembers, StorageLimitMB, ...) تأخذ القيم الافتراضية من الكيان.
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
        Icon = src.Icon,
        Color = src.Color,
        SortOrder = src.SortOrder,
        IsActive = src.IsActive,
        IsDefault = src.IsDefault,
        IsRecommended = false,
        MonthlyPrice = src.MonthlyPrice,
        QuarterlyPrice = src.QuarterlyPrice,
        SemiAnnualPrice = src.SemiAnnualPrice,
        AnnualPrice = src.AnnualPrice,
        Currency = "SAR",
        TrialDays = src.TrialDays,
        GracePeriodDays = src.GracePeriodDays > 0 ? src.GracePeriodDays : 3,
        MaxListings = src.MaxListings,
        MaxImagesPerListing = src.MaxImagesPerListing > 0 ? src.MaxImagesPerListing : 5,
        MaxFeaturedListings = src.MaxFeaturedListings,
        StorageLimitMB = src.StorageLimitMB > 0 ? src.StorageLimitMB : 500,
        CommissionType = MapCommissionType(src.CommissionType),
        CommissionPercentage = src.CommissionPercentage,
        HasVerifiedBadge = src.HasVerifiedBadge,
    };

    private static string MapCommissionType(int legacy) => legacy switch
    {
        0 => "percentage",
        1 => "fixed",
        2 => "tiered",
        _ => "percentage"
    };
}

public static class SubscriptionMapper
{
    /// <summary>
    /// يحوّل LegacySubscription → NewSubscription.
    /// VendorId يُستخدم كـ UserId (مالك الاشتراك هو مالك المتجر).
    /// BillingCycle enum → نص (monthly/quarterly/semiannual/annual).
    /// </summary>
    public static NewSubscription Map(LegacySubscription src, Guid userId) => new()
    {
        Id = src.Id,
        CreatedAt = DateTime.SpecifyKind(src.CreatedAt, DateTimeKind.Utc),
        UpdatedAt = src.UpdatedAt,
        IsDeleted = false,
        UserId = userId,
        PlanId = src.PlanId,
        BillingCycle = MapBillingCycle(src.BillingCycle),
        StartDate = DateTime.SpecifyKind(src.StartDate, DateTimeKind.Utc),
        EndDate = DateTime.SpecifyKind(src.CurrentPeriodEnd, DateTimeKind.Utc),
        TrialEndDate = src.TrialEndDate.HasValue ? DateTime.SpecifyKind(src.TrialEndDate.Value, DateTimeKind.Utc) : null,
        Status = src.Status,
        AmountPaid = src.Price,
        Currency = string.IsNullOrWhiteSpace(src.Currency) ? "SAR" : src.Currency,
        UsedListingsCount = src.CurrentListingsCount,
        LastUsageReset = DateTime.SpecifyKind(src.StartDate, DateTimeKind.Utc),
    };

    private static string MapBillingCycle(int legacy) => legacy switch
    {
        0 => "monthly",
        1 => "quarterly",
        2 => "semiannual",
        3 => "annual",
        _ => "monthly"
    };
}
