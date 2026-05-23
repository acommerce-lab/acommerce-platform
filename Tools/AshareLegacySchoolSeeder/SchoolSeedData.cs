using ACommerce.Catalog.Attributes.Entities;
using ACommerce.Catalog.Attributes.Enums;
using ACommerce.Catalog.Products.Entities;

namespace Ashare.Legacy.SchoolSeeder;

/// <summary>
/// تَعريفات بَيانات بَذر المَدارِس: الفئات الاحترافيّة + الخصائص + قِيَمها +
/// رَبط الفئات بِالخصائص + عُروض المَدارِس العَيِّنَة.
///
/// <para>نَطاق الـ GUID مَعزول عَن البَذر القَديم (الَّذي يَستَخدِم
/// 1*/2*/3*/4*) لِتَجَنُّب أَيّ تَصادُم — المَدارِس تَستَخدِم
/// aa*/bb*/cc*.</para>
///
/// <para>الخصائص الأَساسيّة المُشتَرَكَة (title/description/price/city/
/// location/images/contact flags) <b>تُعاد استِخدامها</b> مِن التَّعريفات
/// القائِمَة (lookup بِالـ Code وَقت التَّشغيل) فَلا تُكَرَّر هُنا. ما هُنا
/// هُوَ الخصائص <b>الاحترافيّة الجَديدَة</b> الخاصّة بِالمَدارِس فَقَط.</para>
/// </summary>
public static class SchoolSeedData
{
    // ─── GUIDs ثابِتَة (idempotent عَبر التَّشغيلات) ──────────────────────
    public static class Ids
    {
        public static readonly Guid CategoryParent     = Guid.Parse("aa000000-0000-0000-5500-000000000001");
        public static readonly Guid CategorySharing    = Guid.Parse("aa000000-0000-0000-5501-000000000001");
        public static readonly Guid CategoryInvestment = Guid.Parse("aa000000-0000-0000-5501-000000000002");
        public static readonly Guid CategoryRent       = Guid.Parse("aa000000-0000-0000-5501-000000000003");
    }

    /// <summary>الأَكواد الأَساسيّة المُشتَرَكَة الَّتي نُعيد استِخدامها مِن
    /// التَّعريفات القائِمَة (لا نُنشِئها — نَبحَث عَنها بِالـ Code).</summary>
    public static readonly string[] ReusedBaseCodes =
    {
        "title", "description", "price", "city", "location", "images",
        "is_phone_allowed", "is_whatsapp_allowed", "is_messaging_allowed"
    };

    // ═══ الفئات ═══════════════════════════════════════════════════════════
    public static List<ProductCategory> Categories(DateTime now) => new()
    {
        new ProductCategory
        {
            Id = Ids.CategoryParent,
            Name = "مدارس", Slug = "schools",
            Description = "فُرَص استثمار وشَراكة وإيجار في القِطاع التَّعليميّ",
            // غير فَعّال: العميل القَديم يَعرِض الفئات بِشَكل مُسَطَّح بِلا
            // هَرَميّة، فَالأب الفارِغ مِن الرَّبط يُعطي نَموذَج إنشاء فارِغاً.
            // الأبناء الثَّلاثَة هُم الفعّالون (لَهُم خصائص + عُروض).
            Icon = "bi-mortarboard", SortOrder = 1, IsActive = false, CreatedAt = now
        },
        new ProductCategory
        {
            Id = Ids.CategoryInvestment, ParentCategoryId = Ids.CategoryParent,
            Name = "مدارس للاستثمار", Slug = "schools-investment",
            Description = "مَدارِس قائِمَة مَعروضَة لِلاستثمار أو الاستحواذ الكامِل",
            Icon = "bi-graph-up-arrow", SortOrder = 2, IsActive = true, CreatedAt = now
        },
        new ProductCategory
        {
            Id = Ids.CategorySharing, ParentCategoryId = Ids.CategoryParent,
            Name = "مدارس للمشاركة", Slug = "schools-sharing",
            Description = "حِصَص شَراكة في مَدارِس قائِمَة أو مَشاريع تَعليميّة",
            Icon = "bi-people", SortOrder = 3, IsActive = true, CreatedAt = now
        },
        new ProductCategory
        {
            Id = Ids.CategoryRent, ParentCategoryId = Ids.CategoryParent,
            Name = "مبانٍ مدرسيّة للإيجار", Slug = "schools-rent",
            Description = "مَبانٍ مُجَهَّزَة كَمَدارِس مَعروضَة لِلإيجار التَّشغيليّ",
            Icon = "bi-building", SortOrder = 4, IsActive = true, CreatedAt = now
        },
    };

    // ═══ الخصائص الجَديدَة + قِيَمها ═══════════════════════════════════════
    // كُلّ خاصّيّة لَها Code فَريد بِبادِئَة school_ لِتَجَنُّب التَّصادُم مَع
    // أَكواد قَديمَة قَد تَبقى (soft-deleted) تَحت فِهرِس فَريد عَلى Code.

    public sealed record AttrSpec(
        Guid Id, string Code, string Name, AttributeType Type,
        bool Required, bool Filterable, int Sort,
        string? ValidationRules = null,
        (string Value, string Display)[]? Values = null);

    public static IReadOnlyList<AttrSpec> NewAttributes()
    {
        Guid A(int n) => Guid.Parse($"bb000000-0000-0000-5502-{n:000000000000}");
        return new List<AttrSpec>
        {
            new(A(1), "school_type", "نوع المدرسة", AttributeType.SingleSelect, true, true, 1,
                Values: new[]{("kindergarten","روضة أطفال"),("primary","ابتدائي"),("intermediate","متوسط"),
                              ("secondary","ثانوي"),("complex","مجمع تعليمي متكامل"),("international","مدرسة دوليّة")}),
            new(A(2), "school_gender", "فئة الطلاب", AttributeType.SingleSelect, true, true, 2,
                Values: new[]{("boys","بنين"),("girls","بنات"),("mixed","مختلط")}),
            new(A(3), "school_curriculum", "المنهج", AttributeType.SingleSelect, false, true, 3,
                Values: new[]{("national","وطنيّ سعوديّ"),("international","دوليّ IB"),("american","أمريكيّ"),
                              ("british","بريطانيّ"),("arabic","عربيّ")}),
            new(A(4), "school_stages", "المراحل الدراسيّة", AttributeType.MultiSelect, false, true, 4,
                Values: new[]{("kg","رياض أطفال"),("primary","ابتدائي"),("intermediate","متوسط"),("secondary","ثانوي")}),
            new(A(5), "school_student_capacity", "السعة الطلابيّة", AttributeType.Number, false, true, 5,
                ValidationRules: "{\"min\":0,\"max\":10000}"),
            new(A(6), "school_classrooms", "عدد الفصول", AttributeType.Number, false, true, 6,
                ValidationRules: "{\"min\":0,\"max\":500}"),
            new(A(7), "school_land_area", "مساحة الأرض (م²)", AttributeType.Number, false, true, 7,
                ValidationRules: "{\"min\":0}"),
            new(A(8), "school_building_area", "مساحة المبنى (م²)", AttributeType.Number, false, true, 8,
                ValidationRules: "{\"min\":0}"),
            new(A(9), "school_floors", "عدد الأدوار", AttributeType.Number, false, false, 9,
                ValidationRules: "{\"min\":0,\"max\":50}"),
            new(A(10), "school_established_year", "سنة التأسيس", AttributeType.Number, false, false, 10,
                ValidationRules: "{\"min\":1900,\"max\":2100}"),
            new(A(11), "school_license_status", "حالة الترخيص", AttributeType.SingleSelect, false, true, 11,
                Values: new[]{("licensed","مرخّصة"),("in_process","تحت الترخيص"),("expired","منتهية")}),
            new(A(12), "school_license_number", "رقم ترخيص وزارة التعليم", AttributeType.Text, false, false, 12),
            new(A(13), "school_facilities", "المرافق", AttributeType.MultiSelect, false, true, 13,
                Values: new[]{("playgrounds","ملاعب رياضيّة"),("science_lab","مختبرات علوم"),("computer_lab","معمل حاسب"),
                              ("library","مكتبة"),("theater","مسرح"),("gym","صالة رياضيّة"),("cafeteria","كافتيريا"),
                              ("prayer_room","مصلّى"),("clinic","عيادة"),("buses","حافلات نقل"),("parking","مواقف"),
                              ("cctv","كاميرات مراقبة")}),
            new(A(14), "school_has_playground", "يوجد ملاعب", AttributeType.Boolean, false, true, 14),
            new(A(15), "school_investment_type", "نوع الفرصة", AttributeType.SingleSelect, true, true, 15,
                Values: new[]{("partnership","شراكة بحصّة"),("full_acquisition","استحواذ كامل"),
                              ("operating_lease","إيجار تشغيليّ"),("financing","تمويل")}),
            new(A(16), "school_investment_amount", "قيمة الفرصة (ر.س)", AttributeType.Number, false, true, 16,
                ValidationRules: "{\"min\":0}"),
            new(A(17), "school_ownership_share", "نسبة الحصّة المعروضة (%)", AttributeType.Number, false, true, 17,
                ValidationRules: "{\"min\":0,\"max\":100}"),
            new(A(18), "school_annual_revenue", "الإيراد السنويّ التقديريّ (ر.س)", AttributeType.Number, false, true, 18,
                ValidationRules: "{\"min\":0}"),
            new(A(19), "school_occupancy_rate", "نسبة الإشغال (%)", AttributeType.Number, false, true, 19,
                ValidationRules: "{\"min\":0,\"max\":100}"),
        };
    }

    /// <summary>رَبط كُلّ فِئَة مَدرَسيّة بِأَكوادِ خَصائِصها (مُشتَرَكَة +
    /// جَديدَة). يُستَخدَم لِبِناء CategoryAttributeMappings — يُحَدِّد
    /// نَموذَج الإدخال في التَّطبيق لِلعُروض المُستَقبَليّة.</summary>
    public static Dictionary<Guid, List<string>> CategoryAttributeCodes() => new()
    {
        [Ids.CategoryInvestment] = new()
        {
            "title","description","school_type","school_gender","school_curriculum","school_stages",
            "school_student_capacity","school_classrooms","school_land_area","school_building_area",
            "school_floors","school_established_year","school_license_status","school_license_number",
            "school_facilities","school_has_playground","school_investment_type","school_investment_amount",
            "school_ownership_share","school_annual_revenue","school_occupancy_rate","price",
            "city","location","is_phone_allowed","is_whatsapp_allowed","is_messaging_allowed","images"
        },
        [Ids.CategorySharing] = new()
        {
            "title","description","school_type","school_gender","school_curriculum","school_stages",
            "school_student_capacity","school_classrooms","school_license_status","school_facilities",
            "school_has_playground","school_investment_type","school_ownership_share","school_investment_amount",
            "school_annual_revenue","school_occupancy_rate","price",
            "city","location","is_phone_allowed","is_whatsapp_allowed","is_messaging_allowed","images"
        },
        [Ids.CategoryRent] = new()
        {
            "title","description","school_type","school_land_area","school_building_area","school_floors",
            "school_classrooms","school_facilities","school_has_playground","price",
            "city","location","is_phone_allowed","is_whatsapp_allowed","is_messaging_allowed","images"
        },
    };

    // ═══ عُروض المَدارِس العَيِّنَة ═════════════════════════════════════════
    // الصُّوَر: روابِط Unsplash CDN عامّة (تَعليم/فُصول/ملاعب/مَبانٍ). البَذر
    // يُنَزِّلها → يَرفَعها عَبر IStorageProvider → يُخَزِّن روابِط الإنتاج.
    // ⚠️ راجِعها أو استَبدِلها قَبل التَّشغيل الفِعليّ لِلإنتاج.

    public sealed record SampleSchool(
        Guid Id, Guid CategoryId, string Title, string Description,
        decimal Price, string City, string Address, double Lat, double Lng,
        Dictionary<string, object> Attributes, string[] ImageUrls);

    public static List<SampleSchool> Samples()
    {
        Guid L(int n) => Guid.Parse($"cc000000-0000-0000-5510-{n:000000000000}");
        const string U = "https://images.unsplash.com/";
        string Img(string id) => $"{U}{id}?w=1600&q=80&auto=format&fit=crop";

        return new()
        {
            new(L(1), Ids.CategoryInvestment,
                "مدرسة أهليّة متكاملة للبيع — حيّ الياسمين، الرياض",
                "مجمّع تعليميّ قائم ومُرخّص (ابتدائي–متوسط–ثانوي) على أرض ٥٠٠٠ م² بإشغال ٨٥٪ وإيرادات سنويّة مستقرّة. فرصة استحواذ كامل بتراخيص سارية ومرافق حديثة تشمل ملاعب ومختبرات ومسرحاً.",
                18500000m, "الرياض", "الرياض، حيّ الياسمين", 24.8260, 46.6400,
                new()
                {
                    ["school_type"]="complex", ["school_gender"]="mixed", ["school_curriculum"]="national",
                    ["school_stages"]=new[]{"primary","intermediate","secondary"},
                    ["school_student_capacity"]=900, ["school_classrooms"]=42,
                    ["school_land_area"]=5000, ["school_building_area"]=7200, ["school_floors"]=3,
                    ["school_established_year"]=2014, ["school_license_status"]="licensed",
                    ["school_license_number"]="EDU-RYD-22841",
                    ["school_facilities"]=new[]{"playgrounds","science_lab","computer_lab","library","theater","cafeteria","prayer_room","buses","parking","cctv"},
                    ["school_has_playground"]=true, ["school_investment_type"]="full_acquisition",
                    ["school_investment_amount"]=18500000, ["school_ownership_share"]=100,
                    ["school_annual_revenue"]=9800000, ["school_occupancy_rate"]=85
                },
                new[]{ Img("photo-1580582932707-520aed937b7b"), Img("photo-1562774053-701939374585"),
                       Img("photo-1503676260728-1c00da094a0b"), Img("photo-1567168544813-cc03465b4fa8") }),

            new(L(2), Ids.CategorySharing,
                "حصّة شراكة ٤٠٪ في مدرسة بنات ناشئة — جدّة",
                "فرصة دخول شريك مشغّل أو مموّل بحصّة ٤٠٪ في مدرسة بنات (روضة وابتدائي) في موقع حيويّ بجدّة. نموّ تسجيل سريع ومنهج وطنيّ مطوّر.",
                3200000m, "جدّة", "جدّة، حيّ الصفا", 21.5810, 39.1925,
                new()
                {
                    ["school_type"]="primary", ["school_gender"]="girls", ["school_curriculum"]="national",
                    ["school_stages"]=new[]{"kg","primary"},
                    ["school_student_capacity"]=420, ["school_classrooms"]=18,
                    ["school_license_status"]="licensed",
                    ["school_facilities"]=new[]{"playgrounds","computer_lab","library","prayer_room","clinic","cctv"},
                    ["school_has_playground"]=true, ["school_investment_type"]="partnership",
                    ["school_ownership_share"]=40, ["school_investment_amount"]=3200000,
                    ["school_annual_revenue"]=4100000, ["school_occupancy_rate"]=72
                },
                new[]{ Img("photo-1588072432836-e10032774350"), Img("photo-1546410531-bb4caa6b424d"),
                       Img("photo-1564981797816-1043664bf78d") }),

            new(L(3), Ids.CategoryRent,
                "مبنى مدرسيّ مجهّز للإيجار التشغيليّ — الدمّام",
                "مبنى مصمّم كمدرسة على أرض ٣٢٠٠ م²، ٢٤ فصلاً، ملاعب ومواقف واسعة، جاهز للتشغيل الفوريّ. مناسب لمشغّل تعليميّ يبحث عن توسّع سريع دون رأس مال إنشائيّ.",
                950000m, "الدمّام", "الدمّام، حيّ الشاطئ", 26.4150, 50.1180,
                new()
                {
                    ["school_type"]="complex", ["school_land_area"]=3200, ["school_building_area"]=4800,
                    ["school_floors"]=2, ["school_classrooms"]=24,
                    ["school_facilities"]=new[]{"playgrounds","science_lab","library","gym","cafeteria","parking","cctv"},
                    ["school_has_playground"]=true
                },
                new[]{ Img("photo-1517825738774-7de9363ef735"), Img("photo-1509062522246-3755977927d7"),
                       Img("photo-1597392582469-a697322d5c16") }),

            new(L(4), Ids.CategoryInvestment,
                "مدرسة دوليّة مرخّصة — فرصة استحواذ، الرياض شمال",
                "مدرسة دوليّة (منهج بريطانيّ) بسمعة قويّة وقائمة انتظار للتسجيل. مرافق متميّزة تشمل مسبحاً ومسرحاً ومختبرات STEM. عوائد تشغيليّة مرتفعة.",
                42000000m, "الرياض", "الرياض، حيّ الملقا", 24.8050, 46.6090,
                new()
                {
                    ["school_type"]="international", ["school_gender"]="mixed", ["school_curriculum"]="british",
                    ["school_stages"]=new[]{"kg","primary","intermediate","secondary"},
                    ["school_student_capacity"]=1200, ["school_classrooms"]=55,
                    ["school_land_area"]=9000, ["school_building_area"]=12000, ["school_floors"]=4,
                    ["school_established_year"]=2010, ["school_license_status"]="licensed",
                    ["school_license_number"]="EDU-RYD-10577",
                    ["school_facilities"]=new[]{"playgrounds","science_lab","computer_lab","library","theater","gym","cafeteria","clinic","buses","parking","cctv"},
                    ["school_has_playground"]=true, ["school_investment_type"]="full_acquisition",
                    ["school_investment_amount"]=42000000, ["school_ownership_share"]=100,
                    ["school_annual_revenue"]=23500000, ["school_occupancy_rate"]=94
                },
                new[]{ Img("photo-1523050854058-8df90110c9f1"), Img("photo-1571260899304-425eee4c7efc"),
                       Img("photo-1497486751825-1233686d5d80"), Img("photo-1592280771190-3e2e4d571952") }),

            new(L(5), Ids.CategorySharing,
                "تمويل توسعة روضة ومرحلة ابتدائيّة — مكّة المكرّمة",
                "مدرسة قائمة تبحث عن شريك مموّل لتوسعة المبنى وإضافة المرحلة المتوسّطة. حصّة ٢٥٪ مقابل تمويل التوسعة، مع خطّة عوائد واضحة.",
                1500000m, "مكّة المكرّمة", "مكّة، حيّ العزيزيّة", 21.4060, 39.8720,
                new()
                {
                    ["school_type"]="primary", ["school_gender"]="mixed", ["school_curriculum"]="national",
                    ["school_stages"]=new[]{"kg","primary"},
                    ["school_student_capacity"]=350, ["school_classrooms"]=15,
                    ["school_license_status"]="licensed",
                    ["school_facilities"]=new[]{"playgrounds","computer_lab","library","prayer_room","cctv"},
                    ["school_has_playground"]=true, ["school_investment_type"]="financing",
                    ["school_ownership_share"]=25, ["school_investment_amount"]=1500000,
                    ["school_annual_revenue"]=2700000, ["school_occupancy_rate"]=68
                },
                new[]{ Img("photo-1577896851231-70ef18881754"), Img("photo-1543269865-cbf427effbad"),
                       Img("photo-1605810230434-7631ac76ec81") }),
        };
    }
}
