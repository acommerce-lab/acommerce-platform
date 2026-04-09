using System.ComponentModel.DataAnnotations.Schema;
using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.Subscriptions.Operations.Abstractions;

namespace Ashare.Api2.Entities;

/// <summary>
/// باقة اشتراك (Subscription Plan) - تطابق بنية باقات عشير الحالية.
/// تطبّق IPlan من مكتبة Subscriptions.Operations العامة.
/// </summary>
public class Plan : IBaseEntity, IPlan
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // === الأساسيات ===
    public string Name { get; set; } = default!;
    public string? NameEn { get; set; }
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public bool IsRecommended { get; set; }

    // === التسعير ===
    public decimal MonthlyPrice { get; set; }
    public decimal? QuarterlyPrice { get; set; }
    public decimal? SemiAnnualPrice { get; set; }
    public decimal? AnnualPrice { get; set; }
    public string Currency { get; set; } = "SAR";
    public int TrialDays { get; set; }
    public int GracePeriodDays { get; set; } = 3;

    // === الحدود ===
    /// <summary>الحد الأقصى للعروض (-1 = غير محدود)</summary>
    public int MaxListings { get; set; }
    public int MaxImagesPerListing { get; set; } = 5;
    public int MaxFeaturedListings { get; set; }
    public int StorageLimitMB { get; set; } = 500;
    public int MaxTeamMembers { get; set; } = 1;
    public int MaxMonthlyMessages { get; set; } = -1;
    public int MaxMonthlyApiCalls { get; set; }
    /// <summary>مدة ظهور العرض بالأيام (0 = غير محدود)</summary>
    public int ListingDurationDays { get; set; }

    // === العمولات ===
    /// <summary>"Percentage" أو "Fixed"</summary>
    public string CommissionType { get; set; } = "Percentage";
    public decimal CommissionPercentage { get; set; }
    public decimal CommissionFixedAmount { get; set; }
    public decimal? MinCommission { get; set; }
    public decimal? MaxCommission { get; set; }

    // === المميزات ===
    public bool HasVerifiedBadge { get; set; }
    public int SearchPriorityBoost { get; set; }
    public string AnalyticsLevel { get; set; } = "Basic"; // None, Basic, Advanced, Full
    public string SupportLevel { get; set; } = "Basic";   // Basic, Standard, Priority
    public bool AllowDirectMessages { get; set; } = true;
    public bool AllowApiAccess { get; set; }
    public bool AllowCustomStorePage { get; set; }
    public bool AllowPromotionalTools { get; set; }
    public bool AllowDataExport { get; set; }
    public bool RemoveBranding { get; set; }
    public bool EmailReports { get; set; }
    public bool PushNotifications { get; set; } = true;

    // === الفئات المسموح بها لهذه الباقة ===
    /// <summary>CSV من slug الفئات: "residential,looking-for-partner". فارغة = كل الفئات.</summary>
    public string? AllowedCategorySlugs { get; set; }

    // ═══════════════════════════════════════════════════════════════
    // تطبيق IPlan - يحوّل الحقول الـAshare-specific إلى صيغة عامة
    // ═══════════════════════════════════════════════════════════════

    /// <summary>الحصص بصيغة عامة - تُبنى من الحقول المحددة لعشير</summary>
    [NotMapped]
    public Dictionary<string, int> Quotas => new()
    {
        ["listings.create"]      = MaxListings,
        ["listings.feature"]     = MaxFeaturedListings,
        ["messages.send"]        = MaxMonthlyMessages,
        ["api.call"]             = MaxMonthlyApiCalls,
        ["team.add_member"]      = MaxTeamMembers,
    };

    /// <summary>النطاقات المسموح بها بصيغة عامة - تربط بين النموذج المحاسبي وحقول عشير</summary>
    [NotMapped]
    public Dictionary<string, string> AllowedScopes => new()
    {
        ["listing_categories"] = AllowedCategorySlugs ?? string.Empty
    };

    // ⚠️ Setter بدون عمل لأن التطبيق يحتفظ بالحقول الـ concrete - الـ Quotas/AllowedScopes
    // تُحسب من الحقول المخصصة. هذه طريقة EF-friendly: لا نخزّن dictionary في DB.
    Dictionary<string, int> IPlan.Quotas
    {
        get => Quotas;
        set { /* مُحسوبة - غير قابلة للضبط مباشرةً */ }
    }
    Dictionary<string, string> IPlan.AllowedScopes
    {
        get => AllowedScopes;
        set { /* مُحسوبة */ }
    }

    /// <summary>الحصول على السعر حسب دورة الفوترة</summary>
    public decimal GetPrice(string billingCycle) => billingCycle switch
    {
        "monthly"    => MonthlyPrice,
        "quarterly"  => QuarterlyPrice ?? MonthlyPrice * 3 * 0.9m,
        "semiannual" => SemiAnnualPrice ?? MonthlyPrice * 6 * 0.85m,
        "annual"     => AnnualPrice ?? MonthlyPrice * 12 * 0.8m,
        _            => MonthlyPrice
    };
}

/// <summary>
/// اشتراك مستخدم في باقة معينة. يطبّق ISubscription من المكتبة العامة.
/// </summary>
public class Subscription : IBaseEntity, ISubscription
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }

    public string BillingCycle { get; set; } = "monthly"; // monthly, quarterly, semiannual, annual

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? TrialEndDate { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;

    public decimal AmountPaid { get; set; }
    public string Currency { get; set; } = "SAR";

    public Guid? PaymentId { get; set; }

    /// <summary>عدّاد الاستخدام (يُعاد تصفيره شهرياً)</summary>
    public int UsedListingsCount { get; set; }
    public int UsedFeaturedListingsCount { get; set; }
    public int UsedMonthlyMessages { get; set; }
    public int UsedMonthlyApiCalls { get; set; }
    public DateTime LastUsageReset { get; set; } = DateTime.UtcNow;

    /// <summary>معرف العملية المحاسبية</summary>
    public Guid? OperationId { get; set; }

    /// <summary>هل الاشتراك نشط حالياً؟</summary>
    public bool IsCurrentlyActive =>
        (Status == SubscriptionStatus.Active || Status == SubscriptionStatus.Trial) &&
        EndDate > DateTime.UtcNow;

    // ═══════════════════════════════════════════════════════════════
    // تطبيق ISubscription من Subscriptions.Operations.Abstractions
    // ═══════════════════════════════════════════════════════════════

    /// <summary>تحويل العدّادات المحددة إلى dictionary عام</summary>
    [NotMapped]
    public Dictionary<string, int> Used
    {
        get => new()
        {
            ["listings.create"]  = UsedListingsCount,
            ["listings.feature"] = UsedFeaturedListingsCount,
            ["messages.send"]    = UsedMonthlyMessages,
            ["api.call"]         = UsedMonthlyApiCalls
        };
        set
        {
            // المعترض يكتب القيم - نُحدّث الحقول الـ concrete
            if (value.TryGetValue("listings.create", out var lc))  UsedListingsCount = lc;
            if (value.TryGetValue("listings.feature", out var lf)) UsedFeaturedListingsCount = lf;
            if (value.TryGetValue("messages.send", out var ms))    UsedMonthlyMessages = ms;
            if (value.TryGetValue("api.call", out var ac))         UsedMonthlyApiCalls = ac;
        }
    }
}

public enum SubscriptionStatus
{
    Pending = 0,
    Trial = 1,
    Active = 2,
    Expired = 3,
    Cancelled = 4,
    Suspended = 5,
    GracePeriod = 6
}
