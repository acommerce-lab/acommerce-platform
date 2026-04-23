namespace Ejar.Api.Services;

/// <summary>
/// بيانات تجريبية ثابتة لتطبيق إيجار. كل القوائم قابلة للتعديل في الذاكرة
/// (Add/Remove/Replace) — لا قاعدة بيانات في وضع التطوير.
/// </summary>
public static class EjarSeed
{
    // ── الحساب التجريبي الحالي ─────────────────────────────────────────
    public const string CurrentUserId = "U-1";

    // ── سجل المستخدمين (هاتف → معرف) ────────────────────────────────────
    private static readonly Dictionary<string, string> _phoneToUser = new()
    {
        ["+966500000001"] = "U-1",
        ["+966500000002"] = "U-2"
    };

    private static readonly Dictionary<string, UserSeed> _users = new()
    {
        ["U-1"] = new("U-1", "سارة محمد العمري",   "+966500000001", true,  "sara@example.com",  true,  "الرياض", new DateTime(2024, 3, 12)),
        ["U-2"] = new("U-2", "خالد عبدالله السالم", "+966500000002", true,  "khaled@example.com", false, "جدة",    new DateTime(2025, 1, 22))
    };

    public static string GetOrCreateUserId(string phone)
    {
        lock (_phoneToUser)
        {
            if (_phoneToUser.TryGetValue(phone, out var uid)) return uid;
            var newId = $"U-{_phoneToUser.Count + 1}";
            _phoneToUser[phone] = newId;
            _users[newId] = new(newId, "", phone, true, "", false, "الرياض", DateTime.UtcNow);
            return newId;
        }
    }

    public static void UpdateUser(string userId, string fullName, string email, string phone, string city)
    {
        lock (_phoneToUser)
        {
            if (!_users.TryGetValue(userId, out var u)) return;
            _users[userId] = u with {
                FullName = fullName, Email = email, Phone = phone, City = city
            };
        }
    }
    public static UserSeed? GetUser(string userId) =>
        _users.TryGetValue(userId, out var u) ? u : null;

    // ── تصنيفات العقارات ──────────────────────────────────────────────
    public static readonly IReadOnlyList<CategorySeed> Categories = new[]
    {
        new CategorySeed("apartment",  "شقة",           "🏢", "residential", new[] { "monthly", "yearly" }),
        new CategorySeed("villa",      "فيلا",          "🏡", "residential", new[] { "monthly", "yearly" }),
        new CategorySeed("room",       "غرفة",          "🛏", "residential", new[] { "monthly" }),
        new CategorySeed("studio",     "استوديو",       "🛋", "residential", new[] { "monthly", "yearly" }),
        new CategorySeed("shop",       "محل تجاري",     "🏪", "commercial",  new[] { "monthly", "yearly" }),
        new CategorySeed("office",     "مكتب",          "🏢", "commercial",  new[] { "monthly", "yearly" }),
        new CategorySeed("warehouse",  "مستودع",        "🏭", "commercial",  new[] { "monthly", "yearly" }),
        new CategorySeed("retreat",    "استراحة",       "🏕", "short_term",  new[] { "daily", "hourly" }),
        new CategorySeed("chalet",     "شاليه",         "🌊", "short_term",  new[] { "daily" }),
        new CategorySeed("hotel_room", "غرفة فندقية",   "🏨", "short_term",  new[] { "daily", "hourly" }),
    };

    // ── المدن ────────────────────────────────────────────────────────────
    public static readonly IReadOnlyList<string> Cities = new[]
    {
        "الرياض", "جدة", "مكة المكرمة", "المدينة المنورة",
        "الدمام", "الخبر", "أبها", "الطائف", "تبوك", "حائل"
    };

    // ── المميزات ──────────────────────────────────────────────────────────
    public static readonly IReadOnlyDictionary<string, string> AmenityLabels =
        new Dictionary<string, string>
        {
            ["ac"]           = "تكييف",
            ["wifi"]         = "واي فاي",
            ["kitchen"]      = "مطبخ",
            ["parking"]      = "موقف سيارة",
            ["elevator"]     = "مصعد",
            ["furnished"]    = "مفروش",
            ["security"]     = "أمن وحراسة",
            ["pool"]         = "مسبح",
            ["gym"]          = "صالة رياضية",
            ["garden"]       = "حديقة",
            ["laundry"]      = "غسالة",
            ["balcony"]      = "شرفة",
            ["private_bath"] = "حمام خاص",
            ["cctv"]         = "كاميرات مراقبة",
            ["generator"]    = "مولد كهربائي",
        };

    public static IReadOnlyList<string> Amenities => AmenityLabels.Keys.ToList();

    // ── الإعلانات ─────────────────────────────────────────────────────────
    public static readonly List<ListingSeed> Listings = new()
    {
        new("L-101", "شقة راقية 3 غرف بحي النخيل",
            "شقة واسعة في حي النخيل الراقي، 3 غرف نوم مع غرفة سائق، مطبخ مجهز، صالة كبيرة، قريبة من الخدمات.",
            3500m, "monthly", "apartment", "الرياض", "حي النخيل",
            24.774, 46.738, new[] { "ac", "wifi", "kitchen", "parking", "elevator", "furnished" },
            "U-2", 3, 2, 180, true, 142, 1),

        new("L-102", "شقة عائلية 2 غرف بحي الروضة",
            "شقة مريحة للعائلة الصغيرة في حي الروضة بجدة، موقع مميز قرب المدارس والمستشفيات.",
            2800m, "monthly", "apartment", "جدة", "حي الروضة",
            21.543, 39.172, new[] { "ac", "kitchen", "parking" },
            "U-2", 2, 1, 120, true, 98, 1),

        new("L-103", "استراحة راقية بحي الربوة",
            "استراحة فاخرة للتجمعات العائلية، مسبح خاص وملعب للأطفال وكامل التجهيزات.",
            1200m, "daily", "retreat", "الرياض", "حي الربوة",
            24.728, 46.657, new[] { "ac", "wifi", "kitchen", "pool", "garden", "cctv", "parking" },
            "U-2", 0, 3, 400, true, 215, 1),

        new("L-104", "فيلا فاخرة 5 غرف بحي الملقا",
            "فيلا دبلوماسية مع حديقة خاصة ومسبح ومواقف متعددة، مناسبة للعائلات الكبيرة.",
            12000m, "monthly", "villa", "الرياض", "حي الملقا",
            24.796, 46.672, new[] { "ac", "wifi", "kitchen", "parking", "pool", "garden", "security", "furnished" },
            "U-1", 5, 4, 650, true, 87, 1),

        new("L-105", "محل تجاري بشارع الأمير محمد",
            "محل تجاري في موقع تجاري مميز على شارع رئيسي، واجهة زجاجية، مناسب لمختلف الأنشطة التجارية.",
            8000m, "monthly", "shop", "جدة", "حي الشرفية",
            21.517, 39.219, new[] { "ac", "parking", "cctv" },
            "U-2", 0, 1, 80, true, 56, 1),

        new("L-106", "شاليه بحري على كورنيش جدة",
            "شاليه مطل مباشرة على البحر الأحمر، تجهيزات فاخرة، مناسب لعائلة من 10 أشخاص.",
            2000m, "daily", "chalet", "جدة", "الكورنيش الشمالي",
            21.629, 39.103, new[] { "ac", "wifi", "kitchen", "pool", "balcony", "cctv" },
            "U-2", 0, 3, 320, true, 303, 1),

        new("L-107", "غرفة فندقية قرب المسجد الحرام",
            "غرفة مكيفة بإطلالة جزئية على المسجد الحرام، مناسبة للزوار والمعتمرين.",
            450m, "daily", "hotel_room", "مكة المكرمة", "حي أجياد",
            21.423, 39.826, new[] { "ac", "wifi", "private_bath" },
            "U-2", 0, 1, 35, true, 189, 1),

        new("L-108", "فيلا سنوية بحي الشاطئ",
            "فيلا متكاملة للإيجار السنوي في حي هادئ بالدمام، قريبة من الكورنيش.",
            150000m, "yearly", "villa", "الدمام", "حي الشاطئ",
            26.428, 50.103, new[] { "ac", "wifi", "kitchen", "parking", "garden", "security" },
            "U-2", 4, 3, 420, false, 34, 1),

        new("L-109", "استوديو للعزاب بحي العليا",
            "استوديو أنيق في قلب الرياض التجاري، مناسب للموظفين والطلاب، موقع ممتاز.",
            1800m, "monthly", "studio", "الرياض", "حي العليا",
            24.688, 46.690, new[] { "ac", "wifi", "kitchen", "elevator", "furnished" },
            "U-2", 0, 1, 55, true, 211, 1),

        new("L-110", "مكتب في برج تجاري بالرياض",
            "مكتب مجهز بالكامل في برج تجاري حديث، خدمات سكرتارية ومؤتمرات مشتركة، موقع مرموق.",
            25000m, "monthly", "office", "الرياض", "طريق الملك فهد",
            24.711, 46.683, new[] { "ac", "wifi", "elevator", "security", "cctv", "parking" },
            "U-2", 0, 2, 160, true, 45, 1),

        new("L-111", "غرفة مستقلة بحي الأندلس",
            "غرفة بحمام خاص في حي هادئ بجدة، مطبخ مشترك نظيف، مناسب للطلاب والموظفين.",
            1200m, "monthly", "room", "جدة", "حي الأندلس",
            21.568, 39.196, new[] { "ac", "wifi", "private_bath", "laundry" },
            "U-2", 0, 1, 28, false, 78, 1),

        new("L-112", "شقة مفروشة بالعزيزية",
            "شقة مفروشة بالكامل للإيجار الشهري، تصلح للعائلات والأفراد العاملين.",
            3000m, "monthly", "apartment", "الدمام", "حي العزيزية",
            26.392, 50.087, new[] { "ac", "wifi", "kitchen", "furnished", "parking" },
            "U-2", 2, 1, 110, true, 65, 1),

        new("L-113", "استراحة عائلية بالطائف",
            "استراحة بمناخ الطائف المعتدل، خضراء وهادئة، مناسبة للعائلات والتجمعات.",
            800m, "daily", "retreat", "الطائف", "حي الحوية",
            21.283, 40.416, new[] { "ac", "kitchen", "garden", "parking", "balcony" },
            "U-1", 0, 2, 280, true, 127, 1),

        new("L-114", "شاليه جبلي بأبها",
            "شاليه في قلب الطبيعة الجبلية بأبها، هواء نقي وإطلالة خلابة، مجهز بالكامل.",
            600m, "daily", "chalet", "أبها", "قرية آل محمد",
            18.216, 42.505, new[] { "kitchen", "parking", "generator", "balcony" },
            "U-2", 0, 2, 220, true, 196, 1),

        new("L-115", "محل تجاري بحي اليرموك",
            "محل في حي تجاري نشط بالخبر، زاوية وواجهتان، مناسب للبيع بالتجزئة والمطاعم.",
            6000m, "monthly", "shop", "الخبر", "حي اليرموك",
            26.283, 50.208, new[] { "ac", "parking", "security" },
            "U-2", 0, 1, 95, false, 42, 1),
    };

    // ── المفضلة ───────────────────────────────────────────────────────────
    public static readonly HashSet<string> FavoriteIds = new() { "L-103", "L-106" };

    // ── المحادثات ─────────────────────────────────────────────────────────
    public static readonly List<ConversationSeed> Conversations = new()
    {
        new("C-1", "خالد السالم", "U-2", "L-104", "استفسار عن فيلا الملقا",
            DateTime.UtcNow.AddHours(-2), 1,
            new List<MessageSeed> {
                new("M-1", "other", "السلام عليكم، هل الفيلا متاحة من أول الشهر؟", DateTime.UtcNow.AddHours(-2)),
                new("M-2", "me",    "وعليكم السلام، نعم متاحة من الأول. يسرنا خدمتك", DateTime.UtcNow.AddHours(-1.5)),
            }),

        new("C-2", "أحمد محمد",  "U-3", "L-109", "استوديو العليا",
            DateTime.UtcNow.AddDays(-1), 0,
            new List<MessageSeed> {
                new("M-3", "other", "هل يوجد مواقف مجانية في المبنى؟", DateTime.UtcNow.AddDays(-1)),
                new("M-4", "me",    "نعم، موقف مجاني لكل وحدة سكنية", DateTime.UtcNow.AddDays(-1).AddMinutes(30)),
            }),
    };

    // ── الإشعارات ─────────────────────────────────────────────────────────
    public static readonly List<NotificationSeed> Notifications = new()
    {
        new("N-1", "رسالة جديدة",         "لديك رسالة من خالد حول فيلا الملقا",   DateTime.UtcNow.AddHours(-2),  false, "C-1", "message"),
        new("N-2", "إعلانك بات نشطاً",    "تم نشر إعلان الفيلا بنجاح",            DateTime.UtcNow.AddDays(-1),   true,  null,  "listing"),
        new("N-3", "تجديد الاشتراك",      "اشتراكك ينتهي خلال 7 أيام",            DateTime.UtcNow.AddDays(-2),   true,  null,  "system"),
        new("N-4", "مشاهدات جديدة",       "إعلانك حصل على 15 مشاهدة اليوم",       DateTime.UtcNow.AddDays(-3),   true,  null,  "listing"),
        new("N-5", "ترحيب بإيجار",        "مرحباً بك في منصة إيجار — ابدأ بنشر إعلانك الأول", DateTime.UtcNow.AddDays(-7), true, null,  "system"),
    };

    // ── الشكاوى ───────────────────────────────────────────────────────────
    public static readonly List<ComplaintSeed> Complaints = new()
    {
        new("X-001", "مشكلة في الدفع",
            "حاولت تجديد الاشتراك لكن العملية لم تكتمل والمبلغ خُصم",
            DateTime.UtcNow.AddDays(-3), "open", "عادي", "S-1",
            new List<ComplaintReplySeed> {
                new("R1", "user",   "حاولت تجديد الاشتراك لكن العملية لم تكتمل والمبلغ خُصم", DateTime.UtcNow.AddDays(-3)),
                new("R2", "admin",  "شكراً لتواصلك. تم فتح تذكرة وسيتواصل معك فريقنا خلال 24 ساعة", DateTime.UtcNow.AddDays(-2)),
            }),
    };

    // ── باقات الاشتراك ────────────────────────────────────────────────────
    public static readonly IReadOnlyList<PlanSeed> Plans = new[]
    {
        new PlanSeed("plan-basic",   "أساسية",   0m,   "شهرياً", 2,  0,  5,  false,
            "مثالية للأفراد الذين يبدؤون في نشر إعلاناتهم",
            new[] { "نشر إعلانين نشطين", "بحث وتصفية متقدمة", "رسائل مع المؤجرين" }),
        new PlanSeed("plan-pro",     "احترافية", 149m, "شهرياً", 10, 2,  15, true,
            "للمؤجرين النشطين الذين يريدون ظهوراً أكبر",
            new[] { "10 إعلانات نشطة", "إعلانان مميزان", "ظهور أعلى في النتائج", "إحصاءات مشاهدات", "دعم مباشر" }),
        new PlanSeed("plan-premium", "بريميوم",  399m, "شهرياً", 50, 10, 30, false,
            "للشركات والوكلاء العقاريين المحترفين",
            new[] { "50 إعلاناً نشطاً", "10 إعلانات مميزة", "ظهور أعلى دائماً", "تقارير تفصيلية", "مدير حساب مخصص" }),
    };

    // ── الاشتراك النشط ───────────────────────────────────────────────────
    public static SubscriptionSeed ActiveSubscription = new(
        "S-1", "plan-pro", "احترافية", "active",
        DateTime.UtcNow.AddMonths(-2), DateTime.UtcNow.AddMonths(1),
        10, 2, 15);

    // ── الفواتير ──────────────────────────────────────────────────────────
    public static readonly List<InvoiceSeed> Invoices = new()
    {
        new("INV-001", "plan-pro", 149m, DateTime.UtcNow.AddMonths(-2), "paid"),
        new("INV-002", "plan-pro", 149m, DateTime.UtcNow.AddMonths(-1), "paid"),
    };

    // ── الصفحات القانونية ─────────────────────────────────────────────────
    public static readonly IReadOnlyList<LegalSeed> Legal = new[]
    {
        new LegalSeed("terms",   "شروط الاستخدام",    "تحكم هذه الشروط استخدامك لمنصة إيجار..."),
        new LegalSeed("privacy", "سياسة الخصوصية",   "نحن في إيجار نحترم خصوصيتك..."),
        new LegalSeed("landlord","إرشادات المؤجرين",  "لضمان تجربة موثوقة، يلتزم المؤجرون بـ..."),
    };

    // ── الإصدار ───────────────────────────────────────────────────────────
    public static readonly VersionSeed Version = new("1.0.0", "1.0.0", false, null, "support@ejar.sa");

    // ── اقتراحات البحث ───────────────────────────────────────────────────
    public static readonly IReadOnlyList<string> PopularSearches = new[]
    {
        "شقة مفروشة الرياض",
        "فيلا للإيجار جدة",
        "استديو مكة باليوم",
        "محل تجاري الدمام",
        "أرض سكنية الخبر",
        "شقة قريبة من النرجس",
        "مكتب إداري الملك فهد"
    };

    public static readonly IReadOnlyList<QuickFilterSeed> QuickFilters = new[]
    {
        new QuickFilterSeed("near_me",   "قريب مني",       "map-pin"),
        new QuickFilterSeed("low_price", "الأقل سعراً",    "tag"),
        new QuickFilterSeed("verified",  "موثّقة",         "check-circle")
    };

    // ═══════════════════════════════════════════════════════════════════════
    // Records
    // ═══════════════════════════════════════════════════════════════════════
    public record UserSeed(
        string Id, string FullName,
        string Phone, bool PhoneVerified,
        string Email, bool EmailVerified,
        string City, DateTime MemberSince);

    public record CategorySeed(
        string Id, string Label, string Emoji,
        string Kind, IReadOnlyList<string> TimeUnits);

    public record ListingSeed(
        string Id, string Title, string Description,
        decimal Price, string TimeUnit, string PropertyType,
        string City, string District,
        double Lat, double Lng,
        IReadOnlyList<string> Amenities,
        string OwnerId,
        int BedroomCount = 0, int BathroomCount = 0, int AreaSqm = 0,
        bool IsVerified = false, int ViewsCount = 0, int Status = 1,
        IReadOnlyList<string>? Images = null);

    public record ConversationSeed(
        string Id, string PartnerName, string PartnerId,
        string ListingId, string Subject, DateTime LastAt, int UnreadCount,
        List<MessageSeed> Messages);

    public record MessageSeed(string Id, string From, string Text, DateTime SentAt);

    public record NotificationSeed(
        string Id, string Title, string Body,
        DateTime CreatedAt, bool IsRead, string? RelatedId, string Type = "system");

    public record ComplaintSeed(
        string Id, string Subject, string Body,
        DateTime CreatedAt, string Status, string Priority,
        string RelatedEntity, List<ComplaintReplySeed> Replies);

    public record ComplaintReplySeed(
        string Id, string From, string Message, DateTime CreatedAt);

    public record PlanSeed(
        string Id, string Name, decimal Price, string Unit,
        int ListingQuota, int FeaturedQuota, int ImagesPerListing,
        bool Popular, string Description, IReadOnlyList<string> Features);

    public record SubscriptionSeed(
        string Id, string PlanId, string PlanName, string Status,
        DateTime StartDate, DateTime EndDate,
        int ListingsLimit, int FeaturedLimit, int ImagesPerListing);

    public record InvoiceSeed(
        string Id, string PlanId, decimal Amount, DateTime Date, string Status);

    public record LegalSeed(string Key, string Title, string Body);

    public record QuickFilterSeed(string Id, string Label, string Icon);

    public record VersionSeed(
        string Current, string Latest, bool IsBlocked,
        string? StoreUrl, string? SupportEmail);
}
