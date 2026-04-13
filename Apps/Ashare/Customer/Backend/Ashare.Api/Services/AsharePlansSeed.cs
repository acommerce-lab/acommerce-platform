using Ashare.Api.Entities;

namespace Ashare.Api.Services;

/// <summary>
/// بيانات باقات عشير - مطابقة لـ Ashare.Shared.AshareSubscriptionPlans (9 باقات).
/// </summary>
public static class AsharePlansSeed
{
    public static class PlanIds
    {
        // باقات المنشآت
        public static readonly Guid BusinessAnnual  = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public static readonly Guid BusinessMonthly = Guid.Parse("10000000-0000-0000-0000-000000000002");
        public static readonly Guid BusinessPack5   = Guid.Parse("10000000-0000-0000-0000-000000000003");
        // باقات الأفراد
        public static readonly Guid IndividualAnnual  = Guid.Parse("20000000-0000-0000-0000-000000000001");
        public static readonly Guid IndividualMonthly = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public static readonly Guid IndividualPack5   = Guid.Parse("20000000-0000-0000-0000-000000000003");
        // باقات خاصة
        public static readonly Guid PartnerSeeker    = Guid.Parse("30000000-0000-0000-0000-000000000001");
        public static readonly Guid CommercialAdmin  = Guid.Parse("30000000-0000-0000-0000-000000000002");
        public static readonly Guid ContractService  = Guid.Parse("30000000-0000-0000-0000-000000000003");
    }

    public static List<Plan> GetAll() => new()
    {
        // ═══════════════════════════════════════════════════════════════════
        // باقات المنشآت والوسطاء
        // ═══════════════════════════════════════════════════════════════════
        new Plan
        {
            Id = PlanIds.BusinessAnnual,
            CreatedAt = DateTime.UtcNow,
            Name = "المنشآت - سنوي",
            NameEn = "Business - Annual",
            Slug = "business-annual",
            Description = "للمنشآت والوسطاء العقاريين - اشتراك سنوي مفتوح",
            DescriptionEn = "For businesses and real estate brokers - unlimited annual subscription",
            Icon = "bi-building", Color = "#8B5CF6",
            SortOrder = 1, IsActive = true, IsRecommended = true,
            MonthlyPrice = 400, AnnualPrice = 4800, Currency = "SAR",
            TrialDays = 7, GracePeriodDays = 7,
            MaxListings = -1, MaxImagesPerListing = 20, MaxFeaturedListings = -1,
            StorageLimitMB = -1, MaxTeamMembers = 10, MaxMonthlyMessages = -1,
            MaxMonthlyApiCalls = -1, ListingDurationDays = 0,
            CommissionType = "Percentage", CommissionPercentage = 3,
            HasVerifiedBadge = true, SearchPriorityBoost = 10,
            AnalyticsLevel = "Full", SupportLevel = "Priority",
            AllowDirectMessages = true, AllowApiAccess = true,
            AllowCustomStorePage = true, AllowPromotionalTools = true,
            AllowDataExport = true, RemoveBranding = true,
            EmailReports = true, PushNotifications = true,
            AllowedCategorySlugs = "residential,looking-for-housing,looking-for-partner,administrative,commercial"
        },
        new Plan
        {
            Id = PlanIds.BusinessMonthly,
            CreatedAt = DateTime.UtcNow,
            Name = "المنشآت - شهري", NameEn = "Business - Monthly", Slug = "business-monthly",
            Description = "للمنشآت والوسطاء العقاريين - اشتراك شهري مفتوح",
            DescriptionEn = "For businesses and real estate brokers - unlimited monthly subscription",
            Icon = "bi-building", Color = "#7C3AED",
            SortOrder = 2, IsActive = true,
            MonthlyPrice = 480, AnnualPrice = 4800, Currency = "SAR",
            GracePeriodDays = 3,
            MaxListings = -1, MaxImagesPerListing = 20, MaxFeaturedListings = -1,
            StorageLimitMB = -1, MaxTeamMembers = 10, MaxMonthlyMessages = -1,
            MaxMonthlyApiCalls = -1,
            CommissionType = "Percentage", CommissionPercentage = 3,
            HasVerifiedBadge = true, SearchPriorityBoost = 8,
            AnalyticsLevel = "Advanced", SupportLevel = "Priority",
            AllowDirectMessages = true, AllowApiAccess = true,
            AllowCustomStorePage = true, AllowPromotionalTools = true,
            AllowDataExport = true,
            EmailReports = true, PushNotifications = true,
            AllowedCategorySlugs = "residential,looking-for-housing,looking-for-partner,administrative,commercial"
        },
        new Plan
        {
            Id = PlanIds.BusinessPack5,
            CreatedAt = DateTime.UtcNow,
            Name = "المنشآت - 5 عقارات", NameEn = "Business - 5 Properties", Slug = "business-pack-5",
            Description = "للمنشآت - باقة 5 عقارات",
            DescriptionEn = "For businesses - 5 properties pack",
            Icon = "bi-box-seam", Color = "#6366F1",
            SortOrder = 3, IsActive = true,
            MonthlyPrice = 140, Currency = "SAR",
            MaxListings = 5, MaxImagesPerListing = 15, MaxFeaturedListings = 1,
            StorageLimitMB = 500, MaxTeamMembers = 3, MaxMonthlyMessages = -1,
            MaxMonthlyApiCalls = 500, ListingDurationDays = 90,
            CommissionType = "Percentage", CommissionPercentage = 3,
            SearchPriorityBoost = 5,
            AnalyticsLevel = "Basic", SupportLevel = "Standard",
            AllowDirectMessages = true, AllowPromotionalTools = true,
            PushNotifications = true,
            AllowedCategorySlugs = "residential,administrative,commercial"
        },

        // ═══════════════════════════════════════════════════════════════════
        // باقات الأفراد
        // ═══════════════════════════════════════════════════════════════════
        new Plan
        {
            Id = PlanIds.IndividualAnnual,
            CreatedAt = DateTime.UtcNow,
            Name = "الأفراد - سنوي", NameEn = "Individual - Annual", Slug = "individual-annual",
            Description = "للأفراد - اشتراك سنوي مفتوح",
            DescriptionEn = "For individuals - unlimited annual subscription",
            Icon = "bi-person", Color = "#10B981",
            SortOrder = 4, IsActive = true, IsRecommended = true,
            MonthlyPrice = 167, AnnualPrice = 2000, Currency = "SAR",
            TrialDays = 7, GracePeriodDays = 7,
            MaxListings = -1, MaxImagesPerListing = 15, MaxFeaturedListings = -1,
            StorageLimitMB = -1, MaxTeamMembers = 1, MaxMonthlyMessages = -1,
            MaxMonthlyApiCalls = -1,
            CommissionType = "Percentage", CommissionPercentage = 3,
            HasVerifiedBadge = true, SearchPriorityBoost = 7,
            AnalyticsLevel = "Advanced", SupportLevel = "Priority",
            AllowDirectMessages = true, AllowCustomStorePage = true,
            AllowPromotionalTools = true, AllowDataExport = true,
            EmailReports = true, PushNotifications = true,
            AllowedCategorySlugs = "residential,looking-for-housing,looking-for-partner"
        },
        new Plan
        {
            Id = PlanIds.IndividualMonthly,
            CreatedAt = DateTime.UtcNow,
            Name = "الأفراد - شهري", NameEn = "Individual - Monthly", Slug = "individual-monthly",
            Description = "للأفراد - اشتراك شهري مفتوح",
            DescriptionEn = "For individuals - unlimited monthly subscription",
            Icon = "bi-person", Color = "#059669",
            SortOrder = 5, IsActive = true,
            MonthlyPrice = 200, AnnualPrice = 2000, Currency = "SAR",
            GracePeriodDays = 3,
            MaxListings = -1, MaxImagesPerListing = 15, MaxFeaturedListings = -1,
            StorageLimitMB = -1, MaxTeamMembers = 1, MaxMonthlyMessages = -1,
            MaxMonthlyApiCalls = -1,
            CommissionType = "Percentage", CommissionPercentage = 3,
            SearchPriorityBoost = 5,
            AnalyticsLevel = "Basic", SupportLevel = "Standard",
            AllowDirectMessages = true, AllowPromotionalTools = true,
            PushNotifications = true,
            AllowedCategorySlugs = "residential,looking-for-housing,looking-for-partner"
        },
        new Plan
        {
            Id = PlanIds.IndividualPack5,
            CreatedAt = DateTime.UtcNow,
            Name = "الأفراد - 5 عقارات", NameEn = "Individual - 5 Properties", Slug = "individual-pack-5",
            Description = "للأفراد - باقة 5 عقارات",
            DescriptionEn = "For individuals - 5 properties pack",
            Icon = "bi-box", Color = "#047857",
            SortOrder = 6, IsActive = true, IsDefault = true,
            MonthlyPrice = 75, Currency = "SAR",
            MaxListings = 5, MaxImagesPerListing = 10, MaxFeaturedListings = 0,
            StorageLimitMB = 200, MaxTeamMembers = 1, MaxMonthlyMessages = -1,
            MaxMonthlyApiCalls = 100, ListingDurationDays = 60,
            CommissionType = "Percentage", CommissionPercentage = 3,
            AnalyticsLevel = "Basic", SupportLevel = "Basic",
            AllowDirectMessages = true,
            PushNotifications = true,
            AllowedCategorySlugs = "residential,looking-for-housing"
        },

        // ═══════════════════════════════════════════════════════════════════
        // باقات خاصة
        // ═══════════════════════════════════════════════════════════════════
        new Plan
        {
            Id = PlanIds.PartnerSeeker,
            CreatedAt = DateTime.UtcNow,
            Name = "الباحث عن شريك", NameEn = "Partner Seeker", Slug = "partner-seeker",
            Description = "للمستأجرين الباحثين عن شريك سكن - الدفع بعد إيجاد الشريك",
            DescriptionEn = "For tenants seeking roommates - pay after finding a partner",
            Icon = "bi-people", Color = "#F59E0B",
            SortOrder = 7, IsActive = true,
            MonthlyPrice = 49, Currency = "SAR",
            MaxListings = 1, MaxImagesPerListing = 5, MaxFeaturedListings = 0,
            StorageLimitMB = 50, MaxTeamMembers = 1, MaxMonthlyMessages = -1,
            MaxMonthlyApiCalls = 50, ListingDurationDays = 30,
            CommissionType = "Fixed", CommissionFixedAmount = 49,
            AnalyticsLevel = "None", SupportLevel = "Basic",
            AllowDirectMessages = true,
            PushNotifications = true,
            AllowedCategorySlugs = "looking-for-partner"  // فقط طلب شريك سكن
        },
        new Plan
        {
            Id = PlanIds.CommercialAdmin,
            CreatedAt = DateTime.UtcNow,
            Name = "الإداري والتجاري", NameEn = "Commercial & Administrative", Slug = "commercial-admin",
            Description = "للعقارات الإدارية والتجارية - 5% من قيمة الإيجار بعد التوقيع",
            DescriptionEn = "For commercial and administrative properties - 5% of rent after signing",
            Icon = "bi-shop", Color = "#EF4444",
            SortOrder = 8, IsActive = true,
            MonthlyPrice = 0, Currency = "SAR",  // بدون رسوم اشتراك
            MaxListings = -1, MaxImagesPerListing = 20, MaxFeaturedListings = 5,
            StorageLimitMB = 1000, MaxTeamMembers = 5, MaxMonthlyMessages = -1,
            MaxMonthlyApiCalls = 500,
            CommissionType = "Percentage", CommissionPercentage = 5,
            HasVerifiedBadge = true, SearchPriorityBoost = 5,
            AnalyticsLevel = "Advanced", SupportLevel = "Priority",
            AllowDirectMessages = true, AllowApiAccess = true,
            AllowCustomStorePage = true, AllowPromotionalTools = true,
            AllowDataExport = true,
            EmailReports = true, PushNotifications = true,
            AllowedCategorySlugs = "administrative,commercial"
        },
        new Plan
        {
            Id = PlanIds.ContractService,
            CreatedAt = DateTime.UtcNow,
            Name = "عقد المنصة", NameEn = "Platform Contract", Slug = "platform-contract",
            Description = "خدمة توثيق العقد عبر المنصة - اختياري",
            DescriptionEn = "Platform contract documentation service - optional",
            Icon = "bi-file-earmark-text", Color = "#6366F1",
            SortOrder = 9, IsActive = true,
            MonthlyPrice = 129, Currency = "SAR",
            MaxListings = 0,
            CommissionType = "Fixed", CommissionFixedAmount = 129,
            AnalyticsLevel = "None", SupportLevel = "Standard",
            AllowDataExport = true,
            PushNotifications = true,
            AllowedCategorySlugs = ""  // ليست باقة عرض
        }
    };
}
