using ACommerce.SharedKernel.Abstractions.DynamicAttributes;

namespace Ashare.Api.Services;

/// <summary>
/// قوالب السمات الديناميكية للفئات الخمس (مستخرجة من AshareSeedDataService الإنتاجي القديم).
/// كل قالب يحدّد الحقول الإضافية بعد الحقول الأساسية للعرض (العنوان، السعر، المدينة، إلخ).
/// </summary>
internal static class AshareCategoryTemplates
{
    // ===== خيارات مشتركة =====
    private static List<AttributeOption> Cities() => new()
    {
        Opt("riyadh", "Riyadh", "الرياض"),
        Opt("jeddah", "Jeddah", "جدة"),
        Opt("dammam", "Dammam", "الدمام"),
        Opt("makkah", "Makkah", "مكة المكرمة"),
        Opt("madinah", "Madinah", "المدينة المنورة"),
        Opt("khobar", "Khobar", "الخبر"),
        Opt("abha", "Abha", "أبها"),
        Opt("taif", "Taif", "الطائف"),
        Opt("buraidah", "Buraidah", "بريدة"),
        Opt("tabuk", "Tabuk", "تبوك"),
    };

    private static List<AttributeOption> Furnished() => new()
    {
        Opt("furnished", "Furnished", "مفروش"),
        Opt("semi", "Semi-furnished", "مفروش جزئياً"),
        Opt("unfurnished", "Unfurnished", "غير مفروش"),
    };

    private static List<AttributeOption> Genders() => new()
    {
        Opt("male", "Male", "ذكر", "person"),
        Opt("female", "Female", "أنثى", "person-dress"),
        Opt("any", "Any", "أي", "people"),
    };

    private static List<AttributeOption> Amenities() => new()
    {
        Opt("wifi", "Wi-Fi", "واي فاي", "wifi"),
        Opt("ac", "Air conditioning", "تكييف", "snow"),
        Opt("elevator", "Elevator", "مصعد", "arrow-up-square"),
        Opt("parking", "Parking", "موقف سيارات", "p-square"),
        Opt("pool", "Pool", "مسبح", "droplet"),
        Opt("gym", "Gym", "صالة رياضية", "bicycle"),
        Opt("security", "24/7 Security", "أمن 24/7", "shield"),
        Opt("kitchen", "Kitchen", "مطبخ", "egg-fried"),
        Opt("laundry", "Laundry", "غسيل", "tornado"),
        Opt("garden", "Garden", "حديقة", "tree"),
    };

    private static List<AttributeOption> ResidentialPropertyTypes() => new()
    {
        Opt("apartment", "Apartment", "شقة", "building"),
        Opt("villa", "Villa", "فيلا", "house"),
        Opt("studio", "Studio", "استوديو", "door-open"),
        Opt("building", "Building", "عمارة", "buildings"),
        Opt("compound", "Compound", "مجمع سكني", "buildings-fill"),
    };

    private static List<AttributeOption> CommercialPropertyTypes() => new()
    {
        Opt("shop", "Shop", "محل", "shop"),
        Opt("warehouse", "Warehouse", "مستودع", "box-seam"),
        Opt("showroom", "Showroom", "معرض", "easel"),
        Opt("restaurant", "Restaurant", "مطعم", "cup-hot"),
        Opt("workshop", "Workshop", "ورشة", "wrench"),
    };

    private static List<AttributeOption> AdminPropertyTypes() => new()
    {
        Opt("office", "Office", "مكتب", "briefcase"),
        Opt("shared_office", "Shared office", "مكتب مشترك", "people"),
        Opt("meeting_room", "Meeting room", "قاعة اجتماعات", "easel2"),
        Opt("full_floor", "Full floor", "طابق كامل", "building-fill-up"),
    };

    private static List<AttributeOption> RentalTypes() => new()
    {
        Opt("private", "Private", "خاص", "key"),
        Opt("shared", "Shared", "مشاركة", "people"),
    };

    private static List<AttributeOption> BillTypes() => new()
    {
        Opt("inclusive", "Inclusive", "شاملة", "check-all"),
        Opt("offer", "On offer", "حسب العرض", "tag"),
        Opt("separate", "Separate", "منفصلة", "list"),
    };

    private static List<AttributeOption> Parking() => new()
    {
        Opt("none", "None", "لا يوجد"),
        Opt("street", "Street parking", "موقف شارع"),
        Opt("private", "Private", "خاص"),
        Opt("garage", "Garage", "جراج"),
    };

    private static List<AttributeOption> WorkingHours() => new()
    {
        Opt("24_7", "24/7", "24/7"),
        Opt("business", "Business hours", "ساعات العمل الرسمية"),
        Opt("flexible", "Flexible", "مرنة"),
        Opt("appointment", "By appointment", "بموعد مسبق"),
    };

    private static List<AttributeOption> Nationalities() => new()
    {
        Opt("saudi", "Saudi", "سعودي"),
        Opt("egyptian", "Egyptian", "مصري"),
        Opt("yemeni", "Yemeni", "يمني"),
        Opt("syrian", "Syrian", "سوري"),
        Opt("jordanian", "Jordanian", "أردني"),
        Opt("sudanese", "Sudanese", "سوداني"),
        Opt("indian", "Indian", "هندي"),
        Opt("pakistani", "Pakistani", "باكستاني"),
        Opt("bengali", "Bangladeshi", "بنغلاديشي"),
        Opt("filipino", "Filipino", "فلبيني"),
        Opt("other", "Other", "أخرى"),
    };

    private static AttributeOption Opt(string v, string en, string ar, string? icon = null)
        => new() { Value = v, Label = en, LabelAr = ar, Icon = icon };

    // ===== القوالب =====

    public static AttributeTemplate Residential() => new()
    {
        Fields = new()
        {
            F("property_type", "Property type", "نوع العقار", "select",
                opts: ResidentialPropertyTypes(), required: true, showInCard: true, sort: 1, icon: "house"),
            F("rental_type", "Rental type", "نوع التأجير", "select",
                opts: RentalTypes(), required: true, showInCard: true, sort: 2, icon: "key"),
            F("bill_type", "Bills", "الفواتير", "select",
                opts: BillTypes(), sort: 3, icon: "receipt"),
            F("furnished", "Furnishing", "الفرش", "select",
                opts: Furnished(), showInCard: true, sort: 4, icon: "lamp"),
            F("rooms", "Rooms", "الغرف", "number",
                showInCard: true, sort: 5, icon: "door-closed", min: 0, max: 50, unit: "غرفة"),
            F("bathrooms", "Bathrooms", "الحمامات", "number",
                showInCard: true, sort: 6, icon: "droplet", min: 0, max: 20, unit: "حمام"),
            F("area", "Area", "المساحة", "decimal",
                showInCard: true, sort: 7, icon: "bounding-box", min: 0, max: 100000, unit: "م²"),
            F("floor", "Floor", "الطابق", "number",
                sort: 8, icon: "layers", min: -3, max: 200),
            F("amenities", "Amenities", "المرافق", "multi",
                opts: Amenities(), sort: 9, icon: "stars"),
            F("gender_pref", "Gender preference", "تفضيل الجنس", "select",
                opts: Genders(), sort: 10, icon: "people"),
        }
    };

    public static AttributeTemplate LookingForHousing() => new()
    {
        Fields = new()
        {
            F("property_type", "Property type", "نوع العقار", "select",
                opts: ResidentialPropertyTypes(), required: true, showInCard: true, sort: 1, icon: "house"),
            F("rooms", "Rooms needed", "عدد الغرف", "number",
                showInCard: true, sort: 2, icon: "door-closed", min: 1, max: 20, unit: "غرفة"),
            F("furnished", "Furnishing", "الفرش", "select",
                opts: Furnished(), sort: 3, icon: "lamp"),
            F("gender_pref", "Gender", "الجنس", "select",
                opts: Genders(), sort: 4, icon: "people"),
            F("min_price", "Min budget", "الحد الأدنى للميزانية", "decimal",
                sort: 5, icon: "cash-coin", min: 0, unit: "ر.س"),
            F("max_price", "Max budget", "الحد الأقصى للميزانية", "decimal",
                showInCard: true, sort: 6, icon: "cash-stack", min: 0, unit: "ر.س"),
            F("amenities", "Required amenities", "المرافق المطلوبة", "multi",
                opts: Amenities(), sort: 7, icon: "stars"),
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
            F("smoking", "Smokes", "مدخّن", "bool",
                sort: 6, icon: "wind"),
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
            F("floor", "Floor", "الطابق", "number",
                sort: 3, icon: "layers", min: -3, max: 200),
            F("capacity", "Capacity", "السعة", "number",
                showInCard: true, sort: 4, icon: "people", min: 1, unit: "شخص"),
            F("parking", "Parking", "المواقف", "select",
                opts: Parking(), sort: 5, icon: "p-square"),
            F("working_hours", "Working hours", "ساعات العمل", "select",
                opts: WorkingHours(), sort: 6, icon: "clock"),
            F("amenities", "Facilities", "المرافق", "multi",
                opts: Amenities(), sort: 7, icon: "stars"),
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
            F("floor", "Floor", "الطابق", "number",
                sort: 3, icon: "layers", min: -3, max: 200),
            F("capacity", "Capacity", "السعة", "number",
                sort: 4, icon: "people", min: 1, unit: "شخص"),
            F("parking", "Parking", "المواقف", "select",
                opts: Parking(), showInCard: true, sort: 5, icon: "p-square"),
            F("working_hours", "Working hours", "ساعات العمل", "select",
                opts: WorkingHours(), sort: 6, icon: "clock"),
            F("amenities", "Facilities", "المرافق", "multi",
                opts: Amenities(), sort: 7, icon: "stars"),
        }
    };

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
