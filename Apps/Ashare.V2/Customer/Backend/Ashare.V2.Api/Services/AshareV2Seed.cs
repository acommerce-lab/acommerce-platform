namespace Ashare.V2.Api.Services;

/// <summary>
/// بذور Ashare.V2 — مقتبسة من بذور إصدار عشير الأوّل (Apps/Ashare/Customer/Backend)
/// مع اختصار للحقول غير المطلوبة في واجهة V2 الحاليّة.
///
/// مبدأ B.5 (ROADMAP): البذور تعكس بيانات إنتاج حقيقيّة (أحياء الرياض،
/// خطوط عرض/طول حقيقيّة). عند ربط V2 بـ DB سنستبدلها بـ SeederAsync.
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

    public static readonly IReadOnlyList<ListingSeed> Listings = BuildListings();

    public static readonly IReadOnlyList<NotificationSeed> Notifications =
    [
        new("N-1", "booking",      "طلب الحجز قيد المراجعة",  "مالك العرض سيتواصل خلال 24 ساعة",        Hours(-2),  false),
        new("N-2", "booking",      "تمّ تأكيد حجزك",         "شقّة حيّ النرجس — من 1 مايو 2026",        Hours(-10), false),
        new("N-3", "message",      "رسالة جديدة من المالك",  "مرحباً، المفتاح جاهز الأسبوع القادم",     Hours(-26), true),
        new("N-4", "listing",      "عرض جديد قريب منك",      "استديو الدرعية — 1800 ر.س / شهر",        Hours(-30), true),
        new("N-5", "promo",        "خصم 10% على الاشتراك",   "كن مُضيفاً مُعتَمَداً بخصم لفترة محدودة",   Hours(-48), true),
        new("N-6", "review",       "تقييم جديد",             "إيمان أضافت تقييماً 5 نجوم لمشاركتك",    Hours(-72), true),
        new("N-7", "system",       "تحديث سياسة الخصوصيّة",  "اطّلع على النصّ الجديد قبل 1 مايو",       Hours(-120), true)
    ];

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

    // ------------------------------------------------------------------
    private static IReadOnlyList<ListingSeed> BuildListings()
    {
        var list = new List<ListingSeed>
        {
            new("L-101", "شقة مفروشة في حي النرجس",                 "شقة غرفتين وصالة، حي النرجس، شمال الرياض. سعر شهري.",
                2500m, "month", "الرياض", "النرجس",   24.872, 46.638, Features("ac","kitchen","wifi"),
                featured: true,  capacity: 3, rating: 4.5m, categoryId: "apartment"),
            new("L-102", "غرفة في شقة طلاب",                        "غرفة مفردة قرب جامعة الملك عبدالعزيز، جدة.",
                 900m, "month", "جدة",    "السلامة",  21.590, 39.168, Features("wifi","ac"),
                featured: true,  capacity: 4, rating: 4.2m, categoryId: "room"),
            new("L-103", "استديو قرب جامعة الملك سعود",             "استديو مفروش بحمام خاص، الدرعية.",
                1800m, "month", "الرياض", "الدرعية",   24.751, 46.605, Features("ac","kitchen","parking"),
                featured: true,  capacity: 2, rating: 4.8m, categoryId: "studio"),
            new("L-201", "سكن عائلي في المزاحمية",                  "شقة ثلاث غرف، حي المزاحمية، مناسبة للعائلات.",
                3200m, "month", "الرياض", "المزاحمية", 24.480, 46.267, Features("ac","parking","laundry"),
                featured: false, capacity: 5, rating: 4.0m, categoryId: "apartment"),
            new("L-202", "شقة يومي قرب الحرم",                      "شقة يوميّة ثلاث غرف، العزيزية، قرب الحرم.",
                 350m, "day",   "مكة",    "العزيزية",  21.395, 39.867, Features("ac","kitchen","wifi","parking"),
                featured: false, capacity: 6, rating: 4.7m, categoryId: "apartment"),
            new("L-203", "غرفة في فيلا مشتركة",                      "غرفة مؤثّثة في فيلا، حي الشاطئ، الدمام.",
                1200m, "month", "الدمام", "الشاطئ",    26.441, 50.108, Features("ac","parking"),
                featured: false, capacity: 4, rating: 4.3m, categoryId: "villa"),
            new("L-204", "استديو في شمال الرياض",                    "استديو صغير مفروش، حي الصحافة.",
                2100m, "month", "الرياض", "الصحافة",   24.797, 46.629, Features("ac","wifi"),
                featured: false, capacity: 2, rating: 4.1m, categoryId: "studio"),
            new("L-205", "غرفة وصالة حيّ العارض",                    "للمشاركة — غرفة وصالة، حي العارض.",
               37000m, "year",  "الرياض", "العارض",    24.872, 46.638, Features("ac","kitchen"),
                featured: false, capacity: 2, rating: 4.0m, categoryId: "shared"),
            new("L-206", "غرفة فاخرة في حيّ غرناطة",                 "غرفة كبيرة بحمام خاص، شرق الرياض.",
                1800m, "month", "الرياض", "غرناطة",    24.793, 46.766, Features("ac","kitchen","parking","wifi"),
                featured: true,  capacity: 1, rating: 4.9m, categoryId: "room"),
            new("L-207", "استديو مفروش في الملقا",                   "استديو يومي مفروش بالكامل، حي الملقا.",
                 550m, "day",   "الرياض", "الملقا",    24.795, 46.628, Features("wifi","ac","kitchen","parking"),
                featured: false, capacity: 2, rating: 4.6m, categoryId: "studio")
        };
        return list;
    }

    private static IReadOnlyList<string> Features(params string[] keys) => keys;
    private static DateTime Hours(double h) => DateTime.UtcNow.AddHours(h);

    public sealed record CategorySeed(string Id, string Label, string Icon);

    public sealed record ListingSeed(
        string Id,
        string Title,
        string Description,
        decimal Price,
        string TimeUnit,
        string City,
        string District,
        double Lat,
        double Lng,
        IReadOnlyList<string> Amenities,
        bool featured = false,
        int capacity = 0,
        decimal rating = 0m,
        string categoryId = "apartment")
    {
        public bool IsFeatured => featured;
        public int Capacity => capacity;
        public decimal Rating => rating;
        public string CategoryId => categoryId;
    }

    public sealed record NotificationSeed(string Id, string Type, string Title, string Body, DateTime CreatedAt, bool IsRead);
    public sealed record QuickFilterSeed(string Id, string Label, string Icon);
}
