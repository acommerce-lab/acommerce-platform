using ACommerce.SharedKernel.Abstractions.DynamicAttributes;

namespace Ashare.Api.Services;

/// <summary>
/// قوالب السمات الديناميكية للفئات الخمس — مطابقة تماماً لبيانات الإنتاج القديمة
/// حتى تُستورد كل العروض بدون فقد أي حقل أو خيار.
/// </summary>
internal static class AshareCategoryTemplates
{
    // ═══════════════════════════════════════════════
    //  خيارات مشتركة
    // ═══════════════════════════════════════════════

    private static List<AttributeOption> Cities() => new()
    {
        Opt("riyadh",         "Riyadh",          "الرياض"),
        Opt("jeddah",         "Jeddah",          "جدة"),
        Opt("makkah",         "Makkah",          "مكة المكرمة"),
        Opt("madinah",        "Madinah",         "المدينة المنورة"),
        Opt("dammam",         "Dammam",          "الدمام"),
        Opt("khobar",         "Khobar",          "الخبر"),
        Opt("dhahran",        "Dhahran",         "الظهران"),
        Opt("taif",           "Taif",            "الطائف"),
        Opt("abha",           "Abha",            "أبها"),
        Opt("tabuk",          "Tabuk",           "تبوك"),
        Opt("buraidah",       "Buraidah",        "بريدة"),
        Opt("khamis_mushait", "Khamis Mushait",  "خميس مشيط"),
        Opt("hail",           "Hail",            "حائل"),
        Opt("najran",         "Najran",          "نجران"),
        Opt("jazan",          "Jazan",           "جازان"),
    };

    private static List<AttributeOption> Furnished() => new()
    {
        Opt("furnished",       "Furnished",       "مفروش"),
        Opt("semi_furnished",  "Semi-furnished",  "مفروش جزئياً"),
        Opt("unfurnished",     "Unfurnished",     "غير مفروش"),
    };

    private static List<AttributeOption> Genders() => new()
    {
        Opt("male",   "Male",   "ذكر",  "person"),
        Opt("female", "Female", "أنثى", "person-dress"),
        Opt("any",    "Any",    "أي",   "people"),
    };

    private static List<AttributeOption> ResidentialAmenities() => new()
    {
        Opt("ac",          "Air conditioning", "تكييف",          "snow"),
        Opt("wifi",        "Wi-Fi",            "واي فاي",        "wifi"),
        Opt("parking",     "Parking",          "موقف سيارات",    "p-square"),
        Opt("elevator",    "Elevator",         "مصعد",           "arrow-up-square"),
        Opt("security",    "24/7 Security",    "أمن 24/7",       "shield"),
        Opt("gym",         "Gym",              "صالة رياضية",    "bicycle"),
        Opt("pool",        "Pool",             "مسبح",           "droplet"),
        Opt("kitchen",     "Kitchen",          "مطبخ",           "egg-fried"),
        Opt("washer",      "Washer",           "غسالة",          "tornado"),
        Opt("balcony",     "Balcony",          "بلكونة",         "border-top"),
        Opt("garden",      "Garden",           "حديقة",          "tree"),
        Opt("maid_room",   "Maid room",        "غرفة خادمة",     "door-closed"),
        Opt("driver_room", "Driver room",      "غرفة سائق",      "door-closed"),
        Opt("storage",     "Storage",          "مستودع",         "box-seam"),
    };

    private static List<AttributeOption> CommercialFacilities() => new()
    {
        Opt("wifi",         "Wi-Fi",              "واي فاي",          "wifi"),
        Opt("ac",           "Air conditioning",   "تكييف",            "snow"),
        Opt("projector",    "Projector",          "بروجكتر",          "projector"),
        Opt("whiteboard",   "Whiteboard",         "سبورة",            "easel2"),
        Opt("video_conf",   "Video conferencing", "مؤتمرات مرئية",    "camera-video"),
        Opt("printer",      "Printer",            "طابعة",            "printer"),
        Opt("kitchen",      "Kitchen",            "مطبخ",             "egg-fried"),
        Opt("reception",    "Reception",          "استقبال",           "person-workspace"),
        Opt("storage",      "Storage",            "مستودع",           "box-seam"),
        Opt("security",     "Security",           "أمن",              "shield"),
        Opt("elevator",     "Elevator",           "مصعد",             "arrow-up-square"),
        Opt("fire_safety",  "Fire safety",        "سلامة حريق",       "fire"),
        Opt("loading_dock", "Loading dock",       "رصيف تحميل",       "truck"),
        Opt("generator",    "Generator",          "مولد كهرباء",      "lightning"),
        Opt("cctv",         "CCTV",               "كاميرات مراقبة",   "camera"),
    };

    // ═══════════════════════════════════════════════
    //  خيارات سكنية
    // ═══════════════════════════════════════════════

    private static List<AttributeOption> PropertyTypes() => new()
    {
        Opt("villa",     "Villa",     "فيلا",    "house"),
        Opt("building",  "Building",  "عمارة",   "buildings"),
    };

    private static List<AttributeOption> UnitTypes() => new()
    {
        Opt("apartment",  "Apartment",   "شقة",        "building"),
        Opt("studio",     "Studio",      "استوديو",    "door-open"),
        Opt("room",       "Room",        "غرفة",       "door-closed"),
        Opt("duplex",     "Duplex",      "دوبلكس",     "house"),
        Opt("penthouse",  "Penthouse",   "بنتهاوس",    "building-fill-up"),
        Opt("full_floor", "Full floor",  "طابق كامل",  "layers"),
        Opt("annex",      "Annex",       "ملحق",       "house-add"),
    };

    private static List<AttributeOption> Floors() => new()
    {
        Opt("basement",      "Basement",      "القبو"),
        Opt("ground",        "Ground floor",  "الدور الأرضي"),
        Opt("first",         "1st floor",     "الدور الأول"),
        Opt("second",        "2nd floor",     "الدور الثاني"),
        Opt("third",         "3rd floor",     "الدور الثالث"),
        Opt("fourth",        "4th floor",     "الدور الرابع"),
        Opt("fifth",         "5th floor",     "الدور الخامس"),
        Opt("roof",          "Roof",          "السطح"),
        Opt("full_building", "Full building", "المبنى كاملاً"),
    };

    private static List<AttributeOption> RentalTypes() => new()
    {
        Opt("full",   "Full",   "كامل",    "key"),
        Opt("shared", "Shared", "مشاركة",  "people"),
    };

    private static List<AttributeOption> BillTypes() => new()
    {
        Opt("offer",   "Offer",   "عرض",  "tag"),
        Opt("request", "Request", "طلب",  "megaphone"),
    };

    // ═══════════════════════════════════════════════
    //  خيارات تجارية / إدارية
    // ═══════════════════════════════════════════════

    private static List<AttributeOption> CommercialPropertyTypes() => new()
    {
        Opt("shop",         "Shop",           "محل",               "shop"),
        Opt("complex",      "Complex",        "مجمع",              "buildings"),
        Opt("mall",         "Mall",           "مول",               "shop-window"),
        Opt("warehouse",    "Warehouse",      "مستودع",            "box-seam"),
        Opt("showroom",     "Showroom",       "معرض",              "easel"),
        Opt("restaurant",   "Restaurant",     "مطعم",              "cup-hot"),
        Opt("kiosk",        "Kiosk",          "كشك",               "cart4"),
        Opt("office",       "Office",         "مكتب",              "briefcase"),
        Opt("coworking",    "Coworking",      "مساحة عمل مشتركة", "people"),
        Opt("meeting_room", "Meeting room",   "قاعة اجتماعات",     "easel2"),
        Opt("event_hall",   "Event hall",     "قاعة مناسبات",      "calendar-event"),
        Opt("clinic",       "Clinic",         "عيادة",             "heart-pulse"),
        Opt("gym",          "Gym",            "صالة رياضية",       "bicycle"),
        Opt("salon",        "Salon",          "صالون",             "scissors"),
        Opt("workshop",     "Workshop",       "ورشة",              "wrench"),
    };

    private static List<AttributeOption> AdminPropertyTypes() => new()
    {
        Opt("office",        "Office",         "مكتب",             "briefcase"),
        Opt("shared_office", "Shared office",  "مكتب مشترك",       "people"),
        Opt("meeting_room",  "Meeting room",   "قاعة اجتماعات",    "easel2"),
        Opt("full_floor",    "Full floor",     "طابق كامل",        "building-fill-up"),
    };

    private static List<AttributeOption> Parking() => new()
    {
        Opt("available",   "Available",   "متوفر",      "p-circle-fill"),
        Opt("limited",     "Limited",     "محدود",      "p-circle"),
        Opt("unavailable", "Unavailable", "غير متوفر",  "x-circle"),
        Opt("paid",        "Paid",        "مدفوع",      "coin"),
    };

    private static List<AttributeOption> WorkingHours() => new()
    {
        Opt("24h",       "24 hours",       "24 ساعة"),
        Opt("business",  "Business hours", "ساعات العمل (8ص–6م)"),
        Opt("extended",  "Extended hours", "ساعات ممتدة (8ص–10م)"),
        Opt("flexible",  "Flexible",       "مرنة"),
    };

    // ═══════════════════════════════════════════════
    //  خيارات شخصية (باحث عن شريك)
    // ═══════════════════════════════════════════════

    private static List<AttributeOption> Nationalities() => new()
    {
        Opt("saudi",       "Saudi",        "سعودي"),
        Opt("emirati",     "Emirati",      "إماراتي"),
        Opt("kuwaiti",     "Kuwaiti",      "كويتي"),
        Opt("qatari",      "Qatari",       "قطري"),
        Opt("bahraini",    "Bahraini",     "بحريني"),
        Opt("omani",       "Omani",        "عُماني"),
        Opt("egyptian",    "Egyptian",     "مصري"),
        Opt("jordanian",   "Jordanian",    "أردني"),
        Opt("syrian",      "Syrian",       "سوري"),
        Opt("lebanese",    "Lebanese",     "لبناني"),
        Opt("yemeni",      "Yemeni",       "يمني"),
        Opt("sudanese",    "Sudanese",     "سوداني"),
        Opt("moroccan",    "Moroccan",     "مغربي"),
        Opt("tunisian",    "Tunisian",     "تونسي"),
        Opt("algerian",    "Algerian",     "جزائري"),
        Opt("iraqi",       "Iraqi",        "عراقي"),
        Opt("palestinian", "Palestinian",  "فلسطيني"),
        Opt("indian",      "Indian",       "هندي"),
        Opt("pakistani",   "Pakistani",    "باكستاني"),
        Opt("bangladeshi", "Bangladeshi",  "بنغلاديشي"),
        Opt("filipino",    "Filipino",     "فلبيني"),
        Opt("indonesian",  "Indonesian",   "إندونيسي"),
        Opt("other",       "Other",        "أخرى"),
    };

    private static List<AttributeOption> SmokingOptions() => new()
    {
        Opt("no",  "No",  "لا",  "check-circle"),
        Opt("yes", "Yes", "نعم", "x-circle"),
    };

    private static AttributeOption Opt(string v, string en, string ar, string? icon = null)
        => new() { Value = v, Label = en, LabelAr = ar, Icon = icon };

    // ═══════════════════════════════════════════════
    //  القوالب
    // ═══════════════════════════════════════════════

    public static AttributeTemplate Residential() => new()
    {
        Fields = new()
        {
            F("property_type", "Property type", "نوع العقار", "select",
                opts: PropertyTypes(), required: true, showInCard: true, sort: 1, icon: "house"),
            F("unit_type", "Unit type", "نوع الوحدة", "select",
                opts: UnitTypes(), showInCard: true, sort: 2, icon: "building"),
            F("rental_type", "Rental type", "نوع التأجير", "select",
                opts: RentalTypes(), required: true, showInCard: true, sort: 3, icon: "key"),
            F("bill_type", "Listing type", "نوع الإعلان", "select",
                opts: BillTypes(), sort: 4, icon: "megaphone"),
            F("furnished", "Furnishing", "الفرش", "select",
                opts: Furnished(), showInCard: true, sort: 5, icon: "lamp"),
            F("rooms", "Rooms", "الغرف", "number",
                showInCard: true, sort: 6, icon: "door-closed", min: 0, max: 50, unit: "غرفة"),
            F("bathrooms", "Bathrooms", "الحمامات", "number",
                showInCard: true, sort: 7, icon: "droplet", min: 0, max: 20, unit: "حمام"),
            F("area", "Area", "المساحة", "decimal",
                showInCard: true, sort: 8, icon: "bounding-box", min: 0, max: 100000, unit: "م²"),
            F("floor", "Floor", "الطابق", "select",
                opts: Floors(), sort: 9, icon: "layers"),
            F("features", "Features", "المرافق", "multi",
                opts: ResidentialAmenities(), sort: 10, icon: "stars"),
            F("gender", "Gender preference", "تفضيل الجنس", "select",
                opts: Genders(), sort: 11, icon: "people"),
            F("requires_license", "Requires license", "يتطلب ترخيص", "bool",
                sort: 12, icon: "shield-check"),
            F("has_owner_license", "Owner has license", "المالك لديه ترخيص", "bool",
                sort: 13, icon: "file-earmark-check"),
        }
    };

    public static AttributeTemplate LookingForHousing() => new()
    {
        Fields = new()
        {
            F("property_type", "Property type", "نوع العقار", "select",
                opts: PropertyTypes(), required: true, showInCard: true, sort: 1, icon: "house"),
            F("unit_type", "Unit type", "نوع الوحدة", "select",
                opts: UnitTypes(), showInCard: true, sort: 2, icon: "building"),
            F("rooms", "Rooms needed", "عدد الغرف", "number",
                showInCard: true, sort: 3, icon: "door-closed", min: 1, max: 20, unit: "غرفة"),
            F("furnished", "Furnishing", "الفرش", "select",
                opts: Furnished(), sort: 4, icon: "lamp"),
            F("gender", "Gender", "الجنس", "select",
                opts: Genders(), sort: 5, icon: "people"),
            F("min_price", "Min budget", "الحد الأدنى للميزانية", "decimal",
                sort: 6, icon: "cash-coin", min: 0, unit: "ر.س"),
            F("max_price", "Max budget", "الحد الأقصى للميزانية", "decimal",
                showInCard: true, sort: 7, icon: "cash-stack", min: 0, unit: "ر.س"),
            F("features", "Required features", "المرافق المطلوبة", "multi",
                opts: ResidentialAmenities(), sort: 8, icon: "stars"),
        }
    };

    public static AttributeTemplate LookingForPartner() => new()
    {
        Fields = new()
        {
            F("personal_name", "Your name", "الاسم", "text",
                required: true, sort: 1, icon: "person", placeholder: "أحمد", placeholderAr: "أحمد"),
            F("age", "Age", "العمر", "number",
                showInCard: true, sort: 2, icon: "calendar", min: 18, max: 99, unit: "سنة"),
            F("gender", "Gender", "الجنس", "select",
                opts: Genders(), required: true, showInCard: true, sort: 3, icon: "people"),
            F("nationality", "Nationality", "الجنسية", "select",
                opts: Nationalities(), sort: 4, icon: "flag"),
            F("job", "Job", "المهنة", "text",
                sort: 5, icon: "briefcase", placeholder: "Software engineer", placeholderAr: "مهندس برمجيات"),
            F("smoking", "Smokes", "مدخّن", "select",
                opts: SmokingOptions(), sort: 6, icon: "wind"),
            F("furnished", "Furnishing preference", "تفضيل الفرش", "select",
                opts: Furnished(), sort: 7, icon: "lamp"),
            F("min_price", "Min share", "الحد الأدنى للمشاركة", "decimal",
                sort: 8, icon: "cash-coin", min: 0, unit: "ر.س"),
            F("max_price", "Max share", "الحد الأقصى للمشاركة", "decimal",
                showInCard: true, sort: 9, icon: "cash-stack", min: 0, unit: "ر.س"),
        }
    };

    public static AttributeTemplate Administrative() => new()
    {
        Fields = new()
        {
            F("property_type", "Space type", "نوع المساحة", "select",
                opts: AdminPropertyTypes(), required: true, showInCard: true, sort: 1, icon: "briefcase"),
            F("area", "Area", "المساحة", "decimal",
                showInCard: true, sort: 2, icon: "bounding-box", min: 0, max: 100000, unit: "م²"),
            F("floor", "Floor", "الطابق", "select",
                opts: Floors(), sort: 3, icon: "layers"),
            F("capacity", "Capacity", "السعة", "number",
                showInCard: true, sort: 4, icon: "people", min: 1, unit: "شخص"),
            F("parking", "Parking", "المواقف", "select",
                opts: Parking(), sort: 5, icon: "p-square"),
            F("working_hours", "Working hours", "ساعات العمل", "select",
                opts: WorkingHours(), sort: 6, icon: "clock"),
            F("facilities", "Facilities", "المرافق", "multi",
                opts: CommercialFacilities(), sort: 7, icon: "stars"),
        }
    };

    public static AttributeTemplate Commercial() => new()
    {
        Fields = new()
        {
            F("property_type", "Space type", "نوع المساحة", "select",
                opts: CommercialPropertyTypes(), required: true, showInCard: true, sort: 1, icon: "shop"),
            F("area", "Area", "المساحة", "decimal",
                showInCard: true, sort: 2, icon: "bounding-box", min: 0, max: 100000, unit: "م²"),
            F("floor", "Floor", "الطابق", "select",
                opts: Floors(), sort: 3, icon: "layers"),
            F("capacity", "Capacity", "السعة", "number",
                sort: 4, icon: "people", min: 1, unit: "شخص"),
            F("parking", "Parking", "المواقف", "select",
                opts: Parking(), showInCard: true, sort: 5, icon: "p-square"),
            F("working_hours", "Working hours", "ساعات العمل", "select",
                opts: WorkingHours(), sort: 6, icon: "clock"),
            F("facilities", "Facilities", "المرافق", "multi",
                opts: CommercialFacilities(), sort: 7, icon: "stars"),
        }
    };

    // ═══════════════════════════════════════════════
    //  مُنشئ الحقول
    // ═══════════════════════════════════════════════

    private static AttributeFieldDefinition F(
        string key, string label, string labelAr, string type,
        List<AttributeOption>? opts = null,
        bool required = false, bool showInCard = false,
        int sort = 0, string? icon = null, string? unit = null,
        string? placeholder = null, string? placeholderAr = null,
        decimal? min = null, decimal? max = null)
        => new()
        {
            Key = key, Label = label, LabelAr = labelAr, Type = type,
            Options = opts ?? new(), Required = required, ShowInCard = showInCard,
            SortOrder = sort, Icon = icon, Unit = unit,
            Placeholder = placeholder, PlaceholderAr = placeholderAr,
            Min = min, Max = max
        };
}
