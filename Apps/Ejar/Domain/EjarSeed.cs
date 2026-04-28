namespace Ejar.Domain;

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
        ["+967771234567"] = "U-1",
        ["+967773456789"] = "U-2"
    };

    private static readonly Dictionary<string, UserSeed> _users = new()
    {
        ["U-1"] = new("U-1", "أمل عبدالله المؤيد",   "+967771234567", true,  "amal@example.ye",   true,  "صنعاء", new DateTime(2024, 3, 12)),
        ["U-2"] = new("U-2", "فهد محمد الجمالي",     "+967773456789", true,  "fahd@example.ye",   false, "عدن",   new DateTime(2025, 1, 22))
    };

    public static string GetOrCreateUserId(string phone)
    {
        lock (_phoneToUser)
        {
            if (_phoneToUser.TryGetValue(phone, out var uid)) return uid;
            var newId = $"U-{_phoneToUser.Count + 1}";
            _phoneToUser[phone] = newId;
            _users[newId] = new(newId, "", phone, true, "", false, "صنعاء", DateTime.UtcNow);
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
        "صنعاء", "عدن", "تعز", "الحديدة", "إب",
        "ذمار", "المكلا", "سيئون", "عمران", "حجة"
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
    // الأسعار بالريال اليمني (YER). الإحداثيات مدن يمنية حقيقية.
    public static readonly List<ListingSeed> Listings = new()
    {
        new("L-101", "شقة راقية 3 غرف بحدّة",
            "شقة واسعة في حي حدّة الراقي بصنعاء، 3 غرف نوم وصالة كبيرة، مطبخ مجهّز، قريبة من الخدمات والمولات.",
            85000m, "monthly", "apartment", "صنعاء", "حدّة",
            15.314, 44.207, new[] { "ac", "wifi", "kitchen", "parking", "elevator", "furnished", "generator" },
            "U-2", 3, 2, 180, true, 142, 1),

        new("L-102", "شقة عائلية 2 غرف بشارع الستين",
            "شقة مريحة للعائلة الصغيرة قرب شارع الستين بصنعاء، موقع مميز قرب المدارس والمستشفيات.",
            55000m, "monthly", "apartment", "صنعاء", "السبعين",
            15.312, 44.190, new[] { "ac", "kitchen", "parking", "generator" },
            "U-2", 2, 1, 120, true, 98, 1),

        new("L-103", "استراحة راقية بسناع الجبلية",
            "استراحة فاخرة للتجمعات العائلية على أطراف صنعاء، حديقة كبيرة وملعب للأطفال وكامل التجهيزات.",
            25000m, "daily", "retreat", "صنعاء", "سناع",
            15.428, 44.290, new[] { "ac", "wifi", "kitchen", "garden", "cctv", "parking", "generator" },
            "U-2", 0, 3, 400, true, 215, 1),

        new("L-104", "فيلا فاخرة 5 غرف بحي الجراف",
            "فيلا في حي الجراف الهادئ بصنعاء، حديقة خاصة ومواقف متعددة، مناسبة للعائلات الكبيرة والمقيمين.",
            260000m, "monthly", "villa", "صنعاء", "الجراف",
            15.401, 44.195, new[] { "ac", "wifi", "kitchen", "parking", "garden", "security", "furnished", "generator" },
            "U-1", 5, 4, 650, true, 87, 1),

        new("L-105", "محل تجاري بشارع الزبيري",
            "محل تجاري في موقع حيوي على شارع الزبيري، واجهة زجاجية كبيرة، مناسب لمختلف الأنشطة التجارية.",
            180000m, "monthly", "shop", "صنعاء", "الزبيري",
            15.350, 44.198, new[] { "ac", "parking", "cctv", "generator" },
            "U-2", 0, 1, 80, true, 56, 1),

        new("L-106", "شاليه بحري على كورنيش عدن",
            "شاليه مطل مباشرة على بحر العرب بكورنيش عدن، تجهيزات فاخرة، مناسب لعائلة حتى 10 أشخاص.",
            45000m, "daily", "chalet", "عدن", "كورنيش الغدير",
            12.795, 44.998, new[] { "ac", "wifi", "kitchen", "balcony", "cctv", "generator" },
            "U-2", 0, 3, 320, true, 303, 1),

        new("L-107", "غرفة فندقية بشارع جمال — تعز",
            "غرفة مكيفة في فندق وسط تعز، إطلالة على جبل صبر، مناسبة للمسافرين ورجال الأعمال.",
            12000m, "daily", "hotel_room", "تعز", "جمال",
            13.578, 44.020, new[] { "ac", "wifi", "private_bath", "generator" },
            "U-2", 0, 1, 35, true, 189, 1),

        new("L-108", "فيلا سنوية بكورنيش الحديدة",
            "فيلا متكاملة للإيجار السنوي في حي هادئ بالحديدة، قريبة من الكورنيش وميناء البحر الأحمر.",
            3500000m, "yearly", "villa", "الحديدة", "السبعة",
            14.795, 42.954, new[] { "ac", "wifi", "kitchen", "parking", "garden", "security", "generator" },
            "U-2", 4, 3, 420, false, 34, 1),

        new("L-109", "استوديو للعزاب بحي عصر",
            "استوديو أنيق في حي عصر التجاري بصنعاء، مناسب للموظفين والطلاب، قريب من الجامعات والأسواق.",
            42000m, "monthly", "studio", "صنعاء", "عصر",
            15.347, 44.222, new[] { "ac", "wifi", "kitchen", "elevator", "furnished", "generator" },
            "U-2", 0, 1, 55, true, 211, 1),

        new("L-110", "مكتب في برج تجاري — حدّة",
            "مكتب مجهز بالكامل في برج تجاري حديث بحدّة، خدمات سكرتارية ومؤتمرات مشتركة، موقع مرموق.",
            350000m, "monthly", "office", "صنعاء", "حدّة",
            15.318, 44.212, new[] { "ac", "wifi", "elevator", "security", "cctv", "parking", "generator" },
            "U-2", 0, 2, 160, true, 45, 1),

        new("L-111", "غرفة مستقلة بحي القاهرة — تعز",
            "غرفة بحمام خاص في حي القاهرة الهادئ بتعز، مطبخ مشترك نظيف، مناسبة للطلاب والموظفين.",
            22000m, "monthly", "room", "تعز", "القاهرة",
            13.585, 44.025, new[] { "wifi", "private_bath", "laundry", "generator" },
            "U-2", 0, 1, 28, false, 78, 1),

        new("L-112", "شقة مفروشة بالمعلا — عدن",
            "شقة مفروشة بالكامل للإيجار الشهري في حي المعلا بعدن، تصلح للعائلات والأفراد العاملين.",
            70000m, "monthly", "apartment", "عدن", "المعلا",
            12.787, 44.991, new[] { "ac", "wifi", "kitchen", "furnished", "parking", "generator" },
            "U-2", 2, 1, 110, true, 65, 1),

        new("L-113", "استراحة في حوض الأشراف — إب",
            "استراحة في مناخ إب المعتدل، طبيعة خضراء وهادئة، مناسبة للعائلات والتجمعات الصيفية.",
            18000m, "daily", "retreat", "إب", "حوض الأشراف",
            13.967, 44.183, new[] { "kitchen", "garden", "parking", "balcony", "generator" },
            "U-1", 0, 2, 280, true, 127, 1),

        new("L-114", "شاليه جبلي بريف ذمار",
            "شاليه في قلب الطبيعة الجبلية بريف ذمار، هواء نقي وإطلالة خلابة، مجهز بالكامل.",
            15000m, "daily", "chalet", "ذمار", "ضواحي ذمار",
            14.546, 44.405, new[] { "kitchen", "parking", "generator", "balcony" },
            "U-2", 0, 2, 220, true, 196, 1),

        new("L-115", "محل تجاري بشارع الميناء — المكلا",
            "محل في موقع تجاري نشط على شارع الميناء بالمكلا، زاوية وواجهتان، مناسب للبيع بالتجزئة والمطاعم.",
            120000m, "monthly", "shop", "المكلا", "خور المكلا",
            14.542, 49.124, new[] { "ac", "parking", "security", "generator" },
            "U-2", 0, 1, 95, false, 42, 1),
    };

    // ── المفضلة ───────────────────────────────────────────────────────────
    public static readonly HashSet<string> FavoriteIds = new() { "L-103", "L-106" };

    // ── المحادثات ─────────────────────────────────────────────────────────
    public static readonly List<ConversationSeed> Conversations = new()
    {
        new("C-1", "فهد الجمالي", "U-2", "L-104", "استفسار عن فيلا الجراف",
            DateTime.UtcNow.AddHours(-2), 1,
            new List<MessageSeed> {
                new("M-1", "C-1", "other", "السلام عليكم، هل الفيلا متاحة من أول الشهر؟", DateTime.UtcNow.AddHours(-2)),
                new("M-2", "C-1", "me",    "وعليكم السلام، نعم متاحة من الأول. يسرنا خدمتك", DateTime.UtcNow.AddHours(-1.5)),
            }),

        new("C-2", "علي صالح",   "U-3", "L-109", "استوديو حي عصر",
            DateTime.UtcNow.AddDays(-1), 0,
            new List<MessageSeed> {
                new("M-3", "C-2", "other", "هل يوجد موقف سيارة في المبنى؟", DateTime.UtcNow.AddDays(-1)),
                new("M-4", "C-2", "me",    "نعم، موقف مخصص لكل وحدة + مولّد كهرباء.", DateTime.UtcNow.AddDays(-1).AddMinutes(30)),
            }),
    };

    // ── الإشعارات ─────────────────────────────────────────────────────────
    public static readonly List<NotificationSeed> Notifications = new()
    {
        new("N-1", "رسالة جديدة",         "لديك رسالة من فهد حول فيلا الجراف",    DateTime.UtcNow.AddHours(-2),  false, "C-1", "message"),
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
        new PlanSeed("plan-pro",     "احترافية", 7500m,  "شهرياً", 10, 2,  15, true,
            "للمؤجرين النشطين الذين يريدون ظهوراً أكبر",
            new[] { "10 إعلانات نشطة", "إعلانان مميزان", "ظهور أعلى في النتائج", "إحصاءات مشاهدات", "دعم مباشر" }),
        new PlanSeed("plan-premium", "بريميوم",  19500m, "شهرياً", 50, 10, 30, false,
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
        new("INV-001", "plan-pro", 7500m, DateTime.UtcNow.AddMonths(-2), "paid"),
        new("INV-002", "plan-pro", 7500m, DateTime.UtcNow.AddMonths(-1), "paid"),
    };

    // ── الصفحات القانونية ─────────────────────────────────────────────────
    public static readonly IReadOnlyList<LegalSeed> Legal = new[]
    {
        new LegalSeed("terms",   "شروط الاستخدام",    "تحكم هذه الشروط استخدامك لمنصة إيجار..."),
        new LegalSeed("privacy", "سياسة الخصوصية",   "نحن في إيجار نحترم خصوصيتك..."),
        new LegalSeed("landlord","إرشادات المؤجرين",  "لضمان تجربة موثوقة، يلتزم المؤجرون بـ..."),
    };

    // ── الإصدار ───────────────────────────────────────────────────────────
    public static readonly VersionSeed Version = new("1.0.0", "1.0.0", false, null, "support@ejar.ye");

    // ── اقتراحات البحث ───────────────────────────────────────────────────
    public static readonly IReadOnlyList<string> PopularSearches = new[]
    {
        "شقة مفروشة صنعاء",
        "فيلا للإيجار عدن",
        "استوديو حدّة بالشهر",
        "محل تجاري الزبيري",
        "غرفة طلاب تعز",
        "شقة قريبة من السبعين",
        "مكتب إداري حدّة"
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
        List<MessageSeed> Messages) : ACommerce.Chat.Operations.IChatConversation
    {
        // The current user is hardcoded as "me" in the seed; partner is PartnerId.
        // Real implementation will hydrate this from the request user identity.
        IReadOnlyList<string> ACommerce.Chat.Operations.IChatConversation.ParticipantPartyIds
            => new[] { "me", PartnerId };
    }

    /// <summary>
    /// Implements <see cref="ACommerce.Chat.Operations.IChatMessage"/> directly so
    /// the chat service can broadcast it without an intermediate DTO (Law 6 amended).
    /// <c>From</c> is mapped to <c>SenderPartyId</c>; <c>Text</c> to <c>Body</c>.
    /// </summary>
    public record MessageSeed(string Id, string ConversationId, string From, string Text, DateTime SentAt)
        : ACommerce.Chat.Operations.IChatMessage
    {
        string ACommerce.Chat.Operations.IChatMessage.SenderPartyId => From;
        string ACommerce.Chat.Operations.IChatMessage.Body          => Text;
        DateTime? ACommerce.Chat.Operations.IChatMessage.ReadAt     => null;
    }

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
