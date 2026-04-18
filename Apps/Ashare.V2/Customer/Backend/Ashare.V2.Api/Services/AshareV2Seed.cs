namespace Ashare.V2.Api.Services;

/// <summary>
/// بذور Ashare.V2 — تغطية كاملة لصفحات عشير القديم.
/// </summary>
internal static class AshareV2Seed
{
    public static readonly IReadOnlyList<CategorySeed> Categories =
    [
        new("apartment", "شقة",     "building"),
        new("room",      "غرفة",    "home"),
        new("studio",    "استديو",  "package"),
        new("villa",     "فيلا",    "store"),
        new("shared",    "مشترك",   "user")
    ];

    public static readonly IReadOnlyList<string> Cities =
    [
        "الرياض", "جدة", "مكة", "المدينة", "الدمام", "الخبر", "القصيم", "أبها"
    ];

    public static readonly IReadOnlyList<string> Amenities =
    [
        "ac", "wifi", "kitchen", "parking", "laundry", "elevator",
        "private_bath", "balcony", "furnished", "security", "gym", "pool"
    ];

    public static readonly Dictionary<string, string> AmenityLabels = new()
    {
        ["ac"] = "تكييف",
        ["wifi"] = "واي-فاي",
        ["kitchen"] = "مطبخ",
        ["parking"] = "موقف سيّارة",
        ["laundry"] = "غسيل",
        ["elevator"] = "مصعد",
        ["private_bath"] = "حمّام خاص",
        ["balcony"] = "شرفة",
        ["furnished"] = "مفروش",
        ["security"] = "حراسة",
        ["gym"] = "صالة رياضيّة",
        ["pool"] = "مسبح"
    };

    public static readonly IReadOnlyList<ListingSeed> Listings = BuildListings();

    public static readonly IReadOnlyList<NotificationSeed> Notifications =
    [
        new("N-1", "booking", "طلب الحجز قيد المراجعة", "مالك العرض سيتواصل خلال 24 ساعة", Hours(-2),   false),
        new("N-2", "booking", "تمّ تأكيد حجزك",          "شقّة حيّ النرجس — من 1 مايو 2026",   Hours(-10),  false),
        new("N-3", "message", "رسالة جديدة من المالك",   "مرحباً، المفتاح جاهز الأسبوع القادم", Hours(-26),  true),
        new("N-4", "listing", "عرض جديد قريب منك",       "استديو الدرعية — 1800 ر.س / شهر",    Hours(-30),  true),
        new("N-5", "promo",   "خصم 10% على الاشتراك",    "كن مُضيفاً مُعتَمَداً بخصم",           Hours(-48),  true),
        new("N-6", "review",  "تقييم جديد",              "إيمان أضافت تقييماً 5 نجوم",         Hours(-72),  true),
        new("N-7", "system",  "تحديث سياسة الخصوصيّة",   "اطّلع على النصّ الجديد قبل 1 مايو",   Hours(-120), true)
    ];

    public static readonly IReadOnlyList<BookingSeed> Bookings =
    [
        new("B-1", "L-101", "شقة مفروشة في حي النرجس", 2500m, Days(7),  1, 2, "pending"),
        new("B-2", "L-103", "استديو قرب جامعة الملك سعود", 1800m, Days(-3), 3, 1, "confirmed"),
        new("B-3", "L-202", "شقة يومي قرب الحرم",       350m,  Days(-20),2, 3, "completed"),
        new("B-4", "L-204", "استديو في شمال الرياض",    2100m, Days(-35),1, 1, "cancelled")
    ];

    public static readonly IReadOnlyList<ConversationSeed> Conversations =
    [
        new("C-1", "أحمد - مالك النرجس", "شقة مفروشة في حي النرجس", Hours(-1), 1,
            [new("م1", "partner",  "أهلاً، هل العرض متاح؟",           Hours(-3)),
             new("م2", "me",       "نعم، ما التواريخ المناسبة؟",       Hours(-2)),
             new("م3", "partner",  "1 مايو — لمدة 30 ليلة",             Hours(-1))]),
        new("C-2", "خدمة العملاء",     "استفسار عن الحجز #B-2",    Hours(-10), 0,
            [new("م1", "me",       "الحجز مؤكَّد لكنّي لم أستلم المفتاح", Hours(-12)),
             new("م2", "partner",  "نعتذر، سيتواصل المالك خلال ساعة",  Hours(-10))])
    ];

    public static readonly IReadOnlyList<ComplaintSeed> Complaints =
    [
        new("X-1", "تأخر في الدخول للسكن",
            "المالك تأخّر 6 ساعات عن الموعد المتّفق في حيّ النرجس يوم 18/4.",
            Days(-3), "open", "عادي", "الحجز #B-1",
            [new("R1", "user",  "طلبتُ من المالك الالتزام بالموعد",     Days(-3)),
             new("R2", "staff", "شكراً للإبلاغ — سنتواصل مع المالك اليوم", Days(-3).AddHours(2)),
             new("R3", "staff", "تم تنبيه المالك، نعتذر عن التأخّر",   Days(-2))]),
        new("X-2", "الخدمة لم تطابق الوصف",
            "لا يوجد تكييف رغم ذكره في الإعلان L-205 — مساحة شاركتُها.",
            Days(-10), "resolved", "عالي", "إعلان L-205",
            [new("R1", "user",  "الإعلان ذكر تكييفاً لم أجده",        Days(-10)),
             new("R2", "staff", "تمّ التحقق، سيُعاد المبلغ خلال 3 أيام", Days(-9)),
             new("R3", "user",  "شكراً، استلمتُ الاسترداد",             Days(-6))])
    ];

    public static readonly IReadOnlyList<PlanSeed> Plans =
    [
        new("basic",      "الأساسيّة",  "إعلان واحد / شهر مجاناً",
            0m,   "month", 1,  0, 5,  false,
            ["إعلان واحد فعّال", "5 صور لكلّ إعلان", "دعم عبر البريد"]),
        new("pro",        "المحترف",   "5 إعلانات + تمييز إعلان واحد",
            99m,  "month", 5,  1, 10, true,
            ["5 إعلانات فعّالة", "10 صور لكلّ إعلان", "تمييز إعلان واحد", "إحصاءات تفصيليّة", "دعم ذو أولويّة"]),
        new("enterprise", "المؤسسات",  "إعلانات غير محدودة + 5 مميّزة",
            399m, "month", 99, 5, 20, false,
            ["إعلانات غير محدودة", "20 صورة لكلّ إعلان", "تمييز 5 إعلانات", "API للتكامل", "مدير حساب مخصّص"])
    ];

    /// <summary>الاشتراك النشط للمستخدم الحاليّ (بذرة).</summary>
    public static readonly SubscriptionSeed ActiveSubscription = new(
        Id: "S-1",
        PlanId: "pro",
        PlanName: "المحترف",
        Status: "active",
        StartDate: Days(-18),
        EndDate:   Days(12),
        ListingsUsed: 3,
        ListingsLimit: 5,
        FeaturedUsed: 1,
        FeaturedLimit: 1,
        ImagesPerListing: 10,
        ApiCallsUsed: 240,
        ApiCallsLimit: 1000);

    public static readonly IReadOnlyList<InvoiceSeed> Invoices =
    [
        new("INV-1001", "pro", 99m, Days(-18), "paid"),
        new("INV-0951", "pro", 99m, Days(-49), "paid"),
        new("INV-0900", "basic", 0m, Days(-80), "paid")
    ];

    /// <summary>بروفايل المستخدم (بذرة — يدعم GET/PUT بشكل in-memory).</summary>
    public static UserProfileSeed Profile = new(
        Id: "U-1",
        FullName: "عبدالله القحطاني",
        Email: "user@example.com",
        EmailVerified: true,
        Phone: "0555123456",
        PhoneVerified: true,
        City: "الرياض",
        AvatarUrl: null,
        MemberSince: Days(-420));

    public static readonly IReadOnlyList<string> PopularSearches =
    [
        "شقة مفروشة الرياض",
        "غرفة جامعة الملك سعود",
        "استديو باليوم مكة",
        "سكن طلاب جدة",
        "فيلا مشتركة الدمام",
        "شقة قريبة من النرجس",
        "سكن عائلي المزاحمية"
    ];

    public static readonly IReadOnlyList<QuickFilterSeed> QuickFilters =
    [
        new("near_me",    "قريب مني",       "map-pin"),
        new("low_price",  "الأقل سعراً",    "tag"),
        new("top_rated",  "الأعلى تقييماً", "star")
    ];

    public static readonly IReadOnlyList<LegalSeed> Legal =
    [
        new("privacy", "سياسة الخصوصيّة",
            "نحترم خصوصيّتك — لا نشارك بياناتك إلا لتنفيذ الخدمة.\n\n" +
            "يمكنك طلب حذف حسابك في أيّ وقت عبر /help."),
        new("terms",   "الشروط والأحكام",
            "بتسجيلك تقبل شروط الاستخدام. العروض مسؤوليّة أصحابها.\n\n" +
            "عشير وسيط تقني ولا يضمن الحجوزات."),
        new("refund",  "سياسة الاسترداد",
            "الحجوزات المؤكّدة قابلة للاسترداد خلال 48 ساعة من الدفع.\n\n" +
            "بعد 48 ساعة لا استرداد إلا بموافقة المالك.")
    ];

    public static readonly VersionInfo Version = new(
        Current: "1.0.0",
        Latest:  "1.0.0",
        IsBlocked: false,
        StoreUrl: "https://play.google.com/store/apps/details?id=com.ashare.ashare",
        SupportEmail: "support@ashare.sa");

    // ------------------------------------------------------------------
    private static IReadOnlyList<ListingSeed> BuildListings()
    {
        var list = new List<ListingSeed>
        {
            new("L-101","شقة مفروشة في حي النرجس","شقة غرفتين وصالة، حي النرجس.",
                2500m, "month", "الرياض", "النرجس", 24.872, 46.638, ["ac","kitchen","wifi"],
                featured: true,  capacity: 3, rating: 4.5m, categoryId: "apartment"),
            new("L-102","غرفة في شقة طلاب","غرفة مفردة قرب جامعة الملك عبدالعزيز.",
                 900m, "month", "جدة", "السلامة", 21.590, 39.168, ["wifi","ac"],
                featured: true,  capacity: 4, rating: 4.2m, categoryId: "room"),
            new("L-103","استديو قرب جامعة الملك سعود","استديو مفروش بحمام خاص.",
                1800m, "month", "الرياض", "الدرعية", 24.751, 46.605, ["ac","kitchen","parking"],
                featured: true,  capacity: 2, rating: 4.8m, categoryId: "studio"),
            new("L-201","سكن عائلي في المزاحمية","شقة ثلاث غرف، مناسبة للعائلات.",
                3200m, "month", "الرياض", "المزاحمية", 24.480, 46.267, ["ac","parking","laundry"],
                featured: false, capacity: 5, rating: 4.0m, categoryId: "apartment"),
            new("L-202","شقة يومي قرب الحرم","شقة يوميّة قرب الحرم.",
                 350m, "day", "مكة", "العزيزية", 21.395, 39.867, ["ac","kitchen","wifi","parking"],
                featured: false, capacity: 6, rating: 4.7m, categoryId: "apartment"),
            new("L-203","غرفة في فيلا مشتركة","غرفة مؤثّثة في فيلا.",
                1200m, "month", "الدمام", "الشاطئ", 26.441, 50.108, ["ac","parking"],
                featured: false, capacity: 4, rating: 4.3m, categoryId: "villa"),
            new("L-204","استديو في شمال الرياض","استديو صغير مفروش.",
                2100m, "month", "الرياض", "الصحافة", 24.797, 46.629, ["ac","wifi"],
                featured: false, capacity: 2, rating: 4.1m, categoryId: "studio"),
            new("L-205","غرفة وصالة حيّ العارض","للمشاركة — غرفة وصالة.",
               37000m, "year", "الرياض", "العارض", 24.872, 46.638, ["ac","kitchen"],
                featured: false, capacity: 2, rating: 4.0m, categoryId: "shared"),
            new("L-206","غرفة فاخرة في حيّ غرناطة","غرفة كبيرة بحمام خاص.",
                1800m, "month", "الرياض", "غرناطة", 24.793, 46.766, ["ac","kitchen","parking","wifi"],
                featured: true,  capacity: 1, rating: 4.9m, categoryId: "room"),
            new("L-207","استديو مفروش في الملقا","استديو يومي مفروش.",
                 550m, "day", "الرياض", "الملقا", 24.795, 46.628, ["wifi","ac","kitchen","parking"],
                featured: false, capacity: 2, rating: 4.6m, categoryId: "studio")
        };
        return list;
    }

    private static DateTime Hours(double h) => DateTime.UtcNow.AddHours(h);
    private static DateTime Days(double d) => DateTime.UtcNow.AddDays(d);

    public sealed record CategorySeed(string Id, string Label, string Icon);

    public sealed record ListingSeed(
        string Id, string Title, string Description,
        decimal Price, string TimeUnit, string City, string District,
        double Lat, double Lng,
        IReadOnlyList<string> Amenities,
        bool featured = false, int capacity = 0, decimal rating = 0m,
        string categoryId = "apartment")
    {
        public bool IsFeatured => featured;
        public int Capacity => capacity;
        public decimal Rating => rating;
        public string CategoryId => categoryId;
    }

    public sealed record NotificationSeed(string Id, string Type, string Title, string Body, DateTime CreatedAt, bool IsRead);
    public sealed record BookingSeed(string Id, string ListingId, string ListingTitle, decimal Total, DateTime StartDate, int Nights, int Guests, string Status);
    public sealed record ConversationSeed(string Id, string PartnerName, string Subject, DateTime LastAt, int UnreadCount, IReadOnlyList<MessageSeed> Messages);
    public sealed record MessageSeed(string Id, string From, string Text, DateTime SentAt);
    public sealed record ComplaintReplySeed(string Id, string From, string Message, DateTime CreatedAt);
    public sealed record ComplaintSeed(string Id, string Subject, string Body, DateTime CreatedAt,
        string Status, string Priority, string RelatedEntity, IReadOnlyList<ComplaintReplySeed> Replies);
    public sealed record PlanSeed(string Id, string Name, string Description, decimal Price, string Unit,
        int ListingQuota, int FeaturedQuota, int ImagesPerListing, bool Popular, IReadOnlyList<string> Features);
    public sealed record SubscriptionSeed(string Id, string PlanId, string PlanName, string Status,
        DateTime StartDate, DateTime EndDate,
        int ListingsUsed, int ListingsLimit, int FeaturedUsed, int FeaturedLimit,
        int ImagesPerListing, int ApiCallsUsed, int ApiCallsLimit);
    public sealed record InvoiceSeed(string Id, string PlanId, decimal Amount, DateTime Date, string Status);
    public sealed record UserProfileSeed(string Id, string FullName, string Email, bool EmailVerified,
        string Phone, bool PhoneVerified, string City, string? AvatarUrl, DateTime MemberSince);
    public sealed record QuickFilterSeed(string Id, string Label, string Icon);
    public sealed record LegalSeed(string Key, string Title, string Body);
    public sealed record VersionInfo(string Current, string Latest, bool IsBlocked, string? StoreUrl, string? SupportEmail);
}
