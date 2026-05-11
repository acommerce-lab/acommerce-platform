using ACommerce.SharedKernel.Domain.DynamicAttributes;

namespace Ashare.V3.Data.Templates;

/// <summary>
/// المَرجِع الكانوني لِقَوالِب سِمات فِئات V3. Bootstrap يَنسَخ هذه إلى
/// جَدول <c>CategoryAttributeTemplates</c> عَلى الإقلاع (idempotent).
///
/// <para><b>كَيف تُضيف فِئَة جَديدَة</b>:</para>
/// <list type="number">
///   <item>أَضِف entry في <see cref="All"/> بِـ slug + version + factory method.</item>
///   <item>اِكتُب method تُرجِع <c>AttributeTemplate</c> بِكُلّ الحُقول.</item>
///   <item>كُلّ حَقل: <c>Key</c> (مَفتاح JSON ثابِت)، <c>Type</c>،
///         <c>Label</c> (EN)، <c>LabelAr</c>، <c>Icon</c> (bootstrap-icons name)،
///         <c>ShowInCard</c> (يَظهَر في chips البِطاقَة)، <c>SortOrder</c>.</item>
///   <item>لِحُقول select/multi: <c>Options</c> كُلُّ option لَه
///         <c>Value</c> + <c>Label</c> + <c>LabelAr</c> + <c>Icon</c>.</item>
/// </list>
///
/// <para><b>تَعديل قالَب قائِم</b>: ارفَع <c>Version</c>. Bootstrap يَكتَشِفه ويُحَدِّث
/// DB row لَو <c>IsLockedByAdmin = false</c>. لوحَة التَحَكُّم تَستَطيع التَعديل
/// مَحَلِّيّاً وتَضَع القُفل لِيُتَجاوَز Bootstrap.</para>
/// </summary>
public static class V3CategoryTemplates
{
    /// <summary>كُلّ القَوالِب الكانونِيَّة. اِزدَد إلى الأَسفَل لا تَستَبدِل.</summary>
    public static readonly IReadOnlyList<(string Slug, int Version, AttributeTemplate Template)> All = new[]
    {
        ("apartment",  1, Apartment()),
        ("villa",      1, Villa()),
        ("commercial", 1, Commercial()),
        ("land",       1, Land()),
    };

    // ─── شَقَّة ───────────────────────────────────────────────────
    private static AttributeTemplate Apartment() => new()
    {
        Fields =
        {
            Num    ("bedrooms",    "Bedrooms",    "غرف النوم",      "door-closed",     show: true,  order: 1),
            Num    ("bathrooms",   "Bathrooms",   "دورات المياه",   "droplet",         show: true,  order: 2),
            Num    ("area_sqm",    "Area",        "المساحة",        "rulers",          show: true,  order: 3, unit: "م²"),
            Num    ("floor",       "Floor",       "الدور",          "building",        show: false, order: 4),
            Num    ("total_floors","Total floors","عدد الأدوار",    "building-fill",   show: false, order: 5),
            Select ("furnished",   "Furnishing",  "الفرش",          "house-fill",      show: true,  order: 6, Furnished()),
            Select ("orientation", "Orientation", "الواجهة",        "compass",         show: false, order: 7, Orientations()),
            Bool   ("balcony",     "Balcony",     "بلكونة",         "border-top",      show: false, order: 8),
            Bool   ("parking",     "Parking",     "موقف سيارات",    "p-square",        show: false, order: 9),
            Multi  ("amenities",   "Amenities",   "المرافق",        "grid",            show: false, order: 10, ResidentialAmenities()),
        }
    };

    // ─── فيلا ─────────────────────────────────────────────────────
    private static AttributeTemplate Villa() => new()
    {
        Fields =
        {
            Num    ("bedrooms",     "Bedrooms",        "غرف النوم",      "door-closed", show: true,  order: 1),
            Num    ("bathrooms",    "Bathrooms",       "دورات المياه",   "droplet",     show: true,  order: 2),
            Num    ("area_sqm",     "Built-up area",   "مساحة البناء",   "rulers",      show: true,  order: 3, unit: "م²"),
            Num    ("land_sqm",     "Land area",       "مساحة الأرض",    "geo",         show: true,  order: 4, unit: "م²"),
            Num    ("floors",       "Number of floors","عدد الأدوار",    "building",    show: false, order: 5),
            Select ("furnished",    "Furnishing",      "الفرش",          "house-fill",  show: true,  order: 6, Furnished()),
            Bool   ("private_pool", "Private pool",    "مسبح خاص",       "droplet-fill",show: false, order: 7),
            Bool   ("garden",       "Garden",          "حديقة",          "tree",        show: false, order: 8),
            Bool   ("maid_room",    "Maid room",       "غرفة خادمة",     "door-closed", show: false, order: 9),
            Multi  ("amenities",    "Amenities",       "المرافق",        "grid",        show: false, order: 10, ResidentialAmenities()),
        }
    };

    // ─── تِجاري ───────────────────────────────────────────────────
    private static AttributeTemplate Commercial() => new()
    {
        Fields =
        {
            Num    ("area_sqm",       "Area",            "المساحة",          "rulers",   show: true,  order: 1, unit: "م²"),
            Select ("commercial_use", "Commercial use",  "الاستخدام التجاري", "shop",    show: true,  order: 2, CommercialUses()),
            Num    ("rooms",          "Rooms",           "عدد الغرف",        "grid-3x3", show: true,  order: 3),
            Bool   ("street_facing",  "Street-facing",   "واجهة شارع",       "signpost", show: false, order: 4),
            Num    ("parking_spots",  "Parking spots",   "مواقف السيارات",   "p-square", show: false, order: 5),
            Multi  ("facilities",     "Facilities",      "التسهيلات",        "grid",     show: false, order: 6, CommercialFacilities()),
        }
    };

    // ─── أَرض ────────────────────────────────────────────────────
    private static AttributeTemplate Land() => new()
    {
        Fields =
        {
            Num    ("area_sqm",   "Area",       "المساحة",      "rulers",   show: true,  order: 1, unit: "م²"),
            Select ("land_type",  "Land type",  "نوع الأرض",   "tree",     show: true,  order: 2, LandTypes()),
            Bool   ("on_street",  "On street",  "على شارع",    "signpost", show: false, order: 3),
            Num    ("street_count","Streets",   "عدد الشوارع",  "signpost-2",show: false, order: 4),
            Bool   ("has_water",  "Water",      "ماء",          "droplet",  show: false, order: 5),
            Bool   ("has_power",  "Electricity","كهرباء",      "lightning",show: false, order: 6),
        }
    };

    // ─── خِيارات مُشتَرَكَة ───────────────────────────────────────
    private static List<AttributeOption> Furnished() => new()
    {
        Opt("furnished",      "Furnished",       "مفروش"),
        Opt("semi_furnished", "Semi-furnished",  "مفروش جزئياً"),
        Opt("unfurnished",    "Unfurnished",     "غير مفروش"),
    };

    private static List<AttributeOption> Orientations() => new()
    {
        Opt("north", "North", "شمال"),
        Opt("south", "South", "جنوب"),
        Opt("east",  "East",  "شرق"),
        Opt("west",  "West",  "غرب"),
        Opt("ne",    "NE",    "شمال شرق"),
        Opt("nw",    "NW",    "شمال غرب"),
        Opt("se",    "SE",    "جنوب شرق"),
        Opt("sw",    "SW",    "جنوب غرب"),
    };

    private static List<AttributeOption> ResidentialAmenities() => new()
    {
        Opt("ac",       "Air conditioning",  "تكييف",          "snow"),
        Opt("wifi",     "Wi-Fi",             "واي فاي",        "wifi"),
        Opt("elevator", "Elevator",          "مصعد",           "arrow-up-square"),
        Opt("security", "24/7 Security",     "أمن 24/7",       "shield"),
        Opt("gym",      "Gym",               "صالة رياضية",    "bicycle"),
        Opt("pool",     "Pool",              "مسبح",           "droplet"),
        Opt("storage",  "Storage",           "مستودع",         "box-seam"),
    };

    private static List<AttributeOption> CommercialUses() => new()
    {
        Opt("office",     "Office",        "مكتب"),
        Opt("shop",       "Shop",          "محل"),
        Opt("warehouse",  "Warehouse",     "مستودع"),
        Opt("restaurant", "Restaurant",    "مطعم"),
        Opt("clinic",     "Clinic",        "عيادة"),
        Opt("showroom",   "Showroom",      "معرض"),
    };

    private static List<AttributeOption> CommercialFacilities() => new()
    {
        Opt("wifi",        "Wi-Fi",              "واي فاي",        "wifi"),
        Opt("ac",          "Air conditioning",   "تكييف",          "snow"),
        Opt("projector",   "Projector",          "بروجكتر",        "projector"),
        Opt("reception",   "Reception",          "استقبال",        "person-workspace"),
        Opt("parking",     "Parking",            "مواقف",          "p-square"),
        Opt("security",    "Security",           "أمن",            "shield"),
    };

    private static List<AttributeOption> LandTypes() => new()
    {
        Opt("residential", "Residential", "سكنية"),
        Opt("commercial",  "Commercial",  "تجارية"),
        Opt("industrial",  "Industrial",  "صناعية"),
        Opt("agricultural","Agricultural","زراعية"),
    };

    // ─── factory helpers ─────────────────────────────────────────
    private static AttributeFieldDefinition Num(string key, string en, string ar, string icon,
                                                bool show, int order, string? unit = null) => new()
    { Key = key, Type = "number", Label = en, LabelAr = ar, Icon = icon, ShowInCard = show, SortOrder = order, Unit = unit };

    private static AttributeFieldDefinition Bool(string key, string en, string ar, string icon,
                                                 bool show, int order) => new()
    { Key = key, Type = "bool", Label = en, LabelAr = ar, Icon = icon, ShowInCard = show, SortOrder = order };

    private static AttributeFieldDefinition Select(string key, string en, string ar, string icon,
                                                   bool show, int order, List<AttributeOption> options) => new()
    { Key = key, Type = "select", Label = en, LabelAr = ar, Icon = icon, ShowInCard = show, SortOrder = order, Options = options };

    private static AttributeFieldDefinition Multi(string key, string en, string ar, string icon,
                                                  bool show, int order, List<AttributeOption> options) => new()
    { Key = key, Type = "multi", Label = en, LabelAr = ar, Icon = icon, ShowInCard = show, SortOrder = order, Options = options };

    private static AttributeOption Opt(string value, string en, string ar, string? icon = null) => new()
    { Value = value, Label = en, LabelAr = ar, Icon = icon };
}
