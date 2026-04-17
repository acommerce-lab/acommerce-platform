using ACommerce.SharedKernel.Abstractions.DynamicAttributes;

namespace AshareMigrator.Templates;

/// <summary>
/// نسخة من قوالب عشير — مرآة لـ Apps/Ashare/.../AshareCategoryTemplates.cs.
/// مُنفصلة لأن Ashare.Api مشروع Web SDK ولا يمكن الاعتماد عليه من Console app.
/// أي تعديل في القوالب الأصلية يجب أن ينعكس هنا.
/// </summary>
public static class AshareTemplates
{
    public static AttributeTemplate Residential() => new()
    {
        Fields = new()
        {
            F("property_type", "نوع العقار", "select", PropertyTypes(), required: true, show: true, sort: 1, icon: "house"),
            F("unit_type", "نوع الوحدة", "select", UnitTypes(), show: true, sort: 2, icon: "building"),
            F("rental_type", "نوع التأجير", "select", RentalTypes(), required: true, show: true, sort: 3, icon: "key"),
            F("bill_type", "نوع الإعلان", "select", BillTypes(), sort: 4, icon: "megaphone"),
            F("furnished", "الفرش", "select", Furnished(), show: true, sort: 5, icon: "lamp"),
            F("rooms", "الغرف", "number", show: true, sort: 6, icon: "door-closed", unit: "غرفة"),
            F("bathrooms", "الحمامات", "number", show: true, sort: 7, icon: "droplet", unit: "حمام"),
            F("area", "المساحة", "decimal", show: true, sort: 8, icon: "bounding-box", unit: "م²"),
            F("floor", "الطابق", "select", Floors(), sort: 9, icon: "layers"),
            F("amenities", "المرافق", "multi", ResidentialAmenities(), sort: 10, icon: "stars"),
            F("gender", "تفضيل الجنس", "select", Genders(), sort: 11, icon: "people"),
        }
    };

    public static AttributeTemplate LookingForHousing() => new()
    {
        Fields = new()
        {
            F("property_type", "نوع العقار", "select", PropertyTypes(), required: true, show: true, sort: 1, icon: "house"),
            F("unit_type", "نوع الوحدة", "select", UnitTypes(), show: true, sort: 2, icon: "building"),
            F("rooms", "عدد الغرف", "number", show: true, sort: 3, icon: "door-closed", unit: "غرفة"),
            F("furnished", "الفرش", "select", Furnished(), sort: 4, icon: "lamp"),
            F("gender", "الجنس", "select", Genders(), sort: 5, icon: "people"),
            F("min_price", "الحد الأدنى للميزانية", "decimal", sort: 6, icon: "cash-coin", unit: "ر.س"),
            F("max_price", "الحد الأقصى للميزانية", "decimal", show: true, sort: 7, icon: "cash-stack", unit: "ر.س"),
            F("amenities", "المرافق المطلوبة", "multi", ResidentialAmenities(), sort: 8, icon: "stars"),
        }
    };

    public static AttributeTemplate LookingForPartner() => new()
    {
        Fields = new()
        {
            F("personal_name", "الاسم", "text", required: true, sort: 1, icon: "person"),
            F("age", "العمر", "number", show: true, sort: 2, icon: "calendar", unit: "سنة"),
            F("gender", "الجنس", "select", Genders(), required: true, show: true, sort: 3, icon: "people"),
            F("nationality", "الجنسية", "select", Nationalities(), sort: 4, icon: "flag"),
            F("job", "المهنة", "text", sort: 5, icon: "briefcase"),
            F("smoking", "مدخّن", "select", SmokingOptions(), sort: 6, icon: "wind"),
            F("furnished", "تفضيل الفرش", "select", Furnished(), sort: 7, icon: "lamp"),
            F("min_price", "الحد الأدنى للمشاركة", "decimal", sort: 8, icon: "cash-coin", unit: "ر.س"),
            F("max_price", "الحد الأقصى للمشاركة", "decimal", show: true, sort: 9, icon: "cash-stack", unit: "ر.س"),
        }
    };

    public static AttributeTemplate Administrative() => new()
    {
        Fields = new()
        {
            F("property_type", "نوع المساحة", "select", AdminPropertyTypes(), required: true, show: true, sort: 1, icon: "briefcase"),
            F("area", "المساحة", "decimal", show: true, sort: 2, icon: "bounding-box", unit: "م²"),
            F("floor", "الطابق", "select", Floors(), sort: 3, icon: "layers"),
            F("capacity", "السعة", "number", show: true, sort: 4, icon: "people", unit: "شخص"),
            F("parking", "المواقف", "select", Parking(), sort: 5, icon: "p-square"),
            F("working_hours", "ساعات العمل", "select", WorkingHours(), sort: 6, icon: "clock"),
            F("facilities", "المرافق", "multi", CommercialFacilities(), sort: 7, icon: "stars"),
        }
    };

    public static AttributeTemplate Commercial() => new()
    {
        Fields = new()
        {
            F("property_type", "نوع المساحة", "select", CommercialPropertyTypes(), required: true, show: true, sort: 1, icon: "shop"),
            F("area", "المساحة", "decimal", show: true, sort: 2, icon: "bounding-box", unit: "م²"),
            F("floor", "الطابق", "select", Floors(), sort: 3, icon: "layers"),
            F("capacity", "السعة", "number", sort: 4, icon: "people", unit: "شخص"),
            F("parking", "المواقف", "select", Parking(), show: true, sort: 5, icon: "p-square"),
            F("working_hours", "ساعات العمل", "select", WorkingHours(), sort: 6, icon: "clock"),
            F("facilities", "المرافق", "multi", CommercialFacilities(), sort: 7, icon: "stars"),
        }
    };

    // ─── قواميس الخيارات ───
    private static List<AttributeOption> PropertyTypes() => new()
    {
        O("villa", "فيلا"), O("building", "عمارة"),
    };

    private static List<AttributeOption> UnitTypes() => new()
    {
        O("apartment", "شقة"), O("studio", "استوديو"), O("room", "غرفة"),
        O("duplex", "دوبلكس"), O("penthouse", "بنتهاوس"), O("full_floor", "طابق كامل"), O("annex", "ملحق"),
    };

    private static List<AttributeOption> Floors() => new()
    {
        O("basement", "القبو"), O("ground", "الدور الأرضي"),
        O("first", "الدور الأول"), O("second", "الدور الثاني"), O("third", "الدور الثالث"),
        O("fourth", "الدور الرابع"), O("fifth", "الدور الخامس"),
        O("roof", "السطح"), O("full_building", "المبنى كاملاً"),
    };

    private static List<AttributeOption> RentalTypes() => new() { O("full", "كامل"), O("shared", "مشاركة") };
    private static List<AttributeOption> BillTypes() => new() { O("offer", "عرض"), O("request", "طلب") };
    private static List<AttributeOption> Furnished() => new()
    {
        O("furnished", "مفروش"), O("semi_furnished", "مفروش جزئياً"), O("unfurnished", "غير مفروش"),
    };
    private static List<AttributeOption> Genders() => new() { O("male", "ذكر"), O("female", "أنثى"), O("any", "أي") };
    private static List<AttributeOption> SmokingOptions() => new() { O("no", "لا"), O("yes", "نعم") };

    private static List<AttributeOption> ResidentialAmenities() => new()
    {
        O("ac", "تكييف"), O("wifi", "واي فاي"), O("parking", "موقف سيارات"), O("elevator", "مصعد"),
        O("security", "أمن 24/7"), O("gym", "صالة رياضية"), O("pool", "مسبح"), O("kitchen", "مطبخ"),
        O("washer", "غسالة"), O("balcony", "بلكونة"), O("garden", "حديقة"),
        O("maid_room", "غرفة خادمة"), O("driver_room", "غرفة سائق"), O("storage", "مستودع"),
    };

    private static List<AttributeOption> CommercialFacilities() => new()
    {
        O("wifi", "واي فاي"), O("ac", "تكييف"), O("projector", "بروجكتر"), O("whiteboard", "سبورة"),
        O("video_conf", "مؤتمرات مرئية"), O("printer", "طابعة"), O("kitchen", "مطبخ"), O("reception", "استقبال"),
        O("storage", "مستودع"), O("security", "أمن"), O("elevator", "مصعد"), O("fire_safety", "سلامة حريق"),
        O("loading_dock", "رصيف تحميل"), O("generator", "مولد كهرباء"), O("cctv", "كاميرات مراقبة"),
    };

    private static List<AttributeOption> CommercialPropertyTypes() => new()
    {
        O("shop", "محل"), O("complex", "مجمع"), O("mall", "مول"), O("warehouse", "مستودع"),
        O("showroom", "معرض"), O("restaurant", "مطعم"), O("kiosk", "كشك"), O("office", "مكتب"),
        O("coworking", "مساحة عمل مشتركة"), O("meeting_room", "قاعة اجتماعات"), O("event_hall", "قاعة مناسبات"),
        O("clinic", "عيادة"), O("gym", "صالة رياضية"), O("salon", "صالون"), O("workshop", "ورشة"),
    };

    private static List<AttributeOption> AdminPropertyTypes() => new()
    {
        O("office", "مكتب"), O("shared_office", "مكتب مشترك"),
        O("meeting_room", "قاعة اجتماعات"), O("full_floor", "طابق كامل"),
    };

    private static List<AttributeOption> Parking() => new()
    {
        O("available", "متوفر"), O("limited", "محدود"), O("unavailable", "غير متوفر"), O("paid", "مدفوع"),
    };

    private static List<AttributeOption> WorkingHours() => new()
    {
        O("24h", "24 ساعة"), O("business", "ساعات العمل"),
        O("extended", "ساعات ممتدة"), O("flexible", "مرنة"),
    };

    private static List<AttributeOption> Nationalities() => new()
    {
        O("saudi", "سعودي"), O("emirati", "إماراتي"), O("kuwaiti", "كويتي"), O("qatari", "قطري"),
        O("bahraini", "بحريني"), O("omani", "عُماني"), O("egyptian", "مصري"), O("jordanian", "أردني"),
        O("syrian", "سوري"), O("lebanese", "لبناني"), O("yemeni", "يمني"), O("sudanese", "سوداني"),
        O("moroccan", "مغربي"), O("tunisian", "تونسي"), O("algerian", "جزائري"), O("iraqi", "عراقي"),
        O("palestinian", "فلسطيني"), O("indian", "هندي"), O("pakistani", "باكستاني"),
        O("bangladeshi", "بنغلاديشي"), O("filipino", "فلبيني"), O("indonesian", "إندونيسي"), O("other", "أخرى"),
    };

    private static AttributeOption O(string v, string ar)
        => new() { Value = v, Label = v, LabelAr = ar };

    private static AttributeFieldDefinition F(
        string key, string labelAr, string type,
        List<AttributeOption>? opts = null,
        bool required = false, bool show = false,
        int sort = 0, string? icon = null, string? unit = null)
        => new()
        {
            Key = key, Label = key, LabelAr = labelAr, Type = type,
            Options = opts ?? new(), Required = required, ShowInCard = show,
            SortOrder = sort, Icon = icon, Unit = unit
        };
}
