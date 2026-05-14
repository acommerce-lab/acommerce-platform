namespace Ejar.Api.Data.Templates;

/// <summary>
/// مَفاتيح ثابِتَة لِربط سِمات البروفايل في جَدول
/// <c>CategoryAttributeMappings</c> المَركَزي. النَّمَط نَفسه الَّذي
/// يَستَخدِمه Ashare V3 لِفِئات المُنتَجات، لكِن بِـ "فِئَة" اصطِناعِيَّة
/// مُخَصَّصَة لِلبروفايل.
///
/// <para>الـ <see cref="ScopeId"/> هو sentinel ثابِت يَتَّخِذ مَكان
/// CategoryId في الجَدول، فَنَستَفيد مِن نَفس مَحَرِّك القَوالِب بِلا
/// جَدول ديناميكي مُنفَصِل.</para>
///
/// <para>Ejar افتِراضي بَسيط: <b>لا</b> سِمات ديناميكِيَّة لِلبروفايل
/// بَعد (UserEntity مُكتَفٍ بِحُقول <c>IUserProfile</c>). نَترُك <c>Defaults</c>
/// فارِغَة، والتَطبيقات الَّتي تُريد سِمات إضافِيَّة تُضيفها عَبر admin
/// SQL أَو تُوَسِّع هذا المَلَفّ.</para>
/// </summary>
public static class EjarProfileAttributes
{
    public static readonly Guid ScopeId = new("00000000-0000-0000-0000-00000E1A0F01");

    public static readonly IReadOnlyList<AttributeSeed> Defaults = new AttributeSeed[]
    {
        new("Bio",         "نُبذَة شَخصِيَّة", "LongText"),
        new("Occupation",  "المِهنَة",         "Text"),
        new("Nationality", "الجِنسِيَّة",      "Text"),
        new("Languages",   "اللُغات",          "Text"),
    };
}

/// <summary>سِمات الإعلانات الديناميكِيَّة في Ejar — <b>لِكُلّ kind</b> في
/// شَجَرَة Taxonomy. الـ store يُعيد القالَب المُناسِب حَسب الـ slug
/// المُمَرَّر (الـ scopeId يُشتَقّ مَن slug عَبر MD5 deterministic).
///
/// <para>الـ <see cref="ForKind"/> يَعيد defaults الـ kind المُحَدَّد. لَو
/// الـ kind غَير مَعروف ⇒ يَعيد <see cref="UniversalDefaults"/> (سِمات
/// عامَّة جِدّاً، مَثَل License/HasMaintenance).</para>
///
/// <para><b>لِماذا per-kind</b>: صاحِب المَصلَحَة لاحَظ أَنّ إعلان باص
/// يَطلُب عَدَد غُرَف النَّوم — لِأَنّ الـ template كانَ واحِداً لِكُلّ
/// الإعلانات. التَّعدُّد per-kind يَحُلّ ذلك.</para></summary>
public static class EjarListingAttributes
{
    /// <summary>سِمات تَنطَبِق عَلى أَيّ نَوع — fallback لَو ما الـ kind
    /// مَعروف. نَترُكها فارِغَة الآن (الـ universal lives في
    /// <see cref="IListing"/>).</summary>
    public static readonly IReadOnlyList<AttributeSeed> UniversalDefaults
        = Array.Empty<AttributeSeed>();

    /// <summary>عَقاري — سَكَني/تِجاري/تَرفيهي.</summary>
    public static readonly IReadOnlyList<AttributeSeed> Realty = new AttributeSeed[]
    {
        new("BedroomCount",    "عَدَد الغُرَف",            "Number"),
        new("BathroomCount",   "عَدَد الحَمّامات",          "Number"),
        new("HallCount",       "عَدَد الصالات",            "Number"),
        new("KitchenCount",    "عَدَد المَطابِخ",           "Number"),
        new("AreaSqm",         "المِساحَة (م²)",            "Number"),
        new("Floor",           "الطابِق",                   "SingleSelect", new[] { "ground","first","second","third","fourth","fifth","sixth","seventh","eighth","ninth","tenth" }),
        new("FloorsTotal",     "عَدَد الطَّوابِق الكُلِّي", "Number"),
        new("AgeYears",        "عُمر العَقار (سَنَوات)",    "Number"),
        new("BuildingType",    "نَوع البِناء",              "SingleSelect", new[] { "tower","house","compound","commercial_building","mall" }),
        new("Furnished",       "التَّأثيث",                 "SingleSelect", new[] { "furnished","unfurnished","semi" }),
        new("Parking",         "المَواقِف",                 "SingleSelect", new[] { "yes","no","covered","street" }),
        new("ParkingSlots",    "عَدَد المَواقِف",           "Number"),
        new("LeaseTerm",       "مُدَّة العَقد الأَدنى",     "SingleSelect", new[] { "daily","weekly","monthly","quarterly","yearly" }),
        new("Deposit",         "مَبلَغ التَّأمين",          "Number"),
        new("CommissionPercent","عُمولَة (%)",              "Number"),
        new("PaymentSchedule", "جَدوَل الدَّفع",            "SingleSelect", new[] { "monthly","quarterly","semi_annual","annual","upfront" }),
        new("UtilitiesIncluded","الفَواتير مَشمولَة",       "SingleSelect", new[] { "all","electricity","water","internet","none" }),
        new("WaterSource",     "مَصدَر المياه",            "SingleSelect", new[] { "public","well","tanker","both" }),
        new("PowerBackup",     "كَهرَباء احتِياطِيَّة",     "SingleSelect", new[] { "generator","solar","battery","ups","none" }),
        new("Elevator",        "مَصعَد",                    "Boolean"),
        new("Balcony",         "شُرفَة",                    "Boolean"),
        new("PrivateEntrance", "مَدخَل خاصّ",               "Boolean"),
        new("GuardService",    "حِراسَة",                  "Boolean"),
        new("Cctv",            "كاميرات مُراقَبَة",        "Boolean"),
        new("Garden",          "حَديقَة",                  "Boolean"),
        new("Pool",            "مَسبَح",                   "Boolean"),
        new("MajlisRoom",      "مَجلِس / دِيوانِيَّة",      "Boolean"),
        new("MaidsRoom",       "غُرفَة خادِمَة",            "Boolean"),
        new("StorageRoom",     "غُرفَة تَخزين",             "Boolean"),
        new("View",            "الإطلالَة",                 "SingleSelect", new[] { "sea","mountain","city","garden","street","none" }),
        new("Orientation",     "اتِّجاه العَقار",           "SingleSelect", new[] { "north","south","east","west","northeast","northwest","southeast","southwest" }),
        new("Condition",       "حالَة العَقار",             "SingleSelect", new[] { "new","excellent","good","needs_renovation" }),
        new("PetsPolicy",      "الحَيَوانات الأَليفَة",     "SingleSelect", new[] { "allowed","not_allowed","case_by_case" }),
        new("SmokingPolicy",   "التَّدخين",                 "SingleSelect", new[] { "allowed","outdoor_only","not_allowed" }),
        new("FamiliesOnly",    "لِلعائِلات فَقَط",          "Boolean"),
        new("BachelorsAllowed","يَقبَل عُزّاب",             "Boolean"),
        new("AvailableFrom",   "مُتاح اعتِباراً مَن",        "Date"),
    };

    /// <summary>مَركَبات — سَيّارات/باصات/درّاجات/دَينات.</summary>
    public static readonly IReadOnlyList<AttributeSeed> Vehicle = new AttributeSeed[]
    {
        new("Make",            "الصُنع",                   "Text"),
        new("Model",           "المُوديل",                 "Text"),
        new("Year",            "سَنَة الصُنع",             "Number"),
        new("RegistrationYear","سَنَة التَّسجيل",          "Number"),
        new("Color",           "اللَّون",                  "Text"),
        new("BodyType",        "هَيكَل المَركَبَة",         "SingleSelect", new[] { "sedan","suv","hatchback","coupe","pickup","van","bus","minibus","motorcycle","scooter" }),
        new("Mileage",         "العَدّاد (كم)",            "Number"),
        new("EngineSize",      "سَعَة المُحَرِّك (سي سي)",  "Number"),
        new("Cylinders",       "عَدَد الأُسطُوانات",        "Number"),
        new("FuelType",        "الوَقود",                   "SingleSelect", new[] { "petrol","diesel","electric","hybrid","gas" }),
        new("Transmission",    "ناقِل الحَرَكَة",            "SingleSelect", new[] { "automatic","manual","cvt","dual_clutch" }),
        new("DriveType",       "نَوع الدَّفع",              "SingleSelect", new[] { "fwd","rwd","awd","4wd" }),
        new("Capacity",        "عَدَد الرُكّاب",            "Number"),
        new("Doors",           "عَدَد الأَبواب",            "Number"),
        new("HasDriver",       "يَتَوَفَّر سائِق",          "Boolean"),
        new("DriverIncluded",  "السائِق مَشمول بِالسِعر",   "Boolean"),
        new("FuelIncluded",    "الوَقود مَشمول",            "Boolean"),
        new("InsuranceIncluded","التَّأمين مَشمول",         "Boolean"),
        new("MinimumAge",      "الحَدّ الأَدنى لِعُمر المُستَأجِر","Number"),
        new("LicenseRequired", "نَوع الرُّخصَة المَطلوبَة",  "SingleSelect", new[] { "light","heavy","public_transport","motorcycle" }),
        new("DepositAmount",   "التَّأمين",                 "Number"),
        new("DailyKmLimit",    "الحَدّ اليَومي (كم)",        "Number"),
        new("CrossCityAllowed","يُسمَح بِالسَّفَر بَين المُدُن","Boolean"),
        new("PickupAvailable", "خِدمَة التَّوصيل",          "Boolean"),
        new("AirConditioning", "تَكييف",                    "Boolean"),
        new("Bluetooth",       "بلوتوث",                    "Boolean"),
        new("Gps",             "نِظام مَلاحَة (GPS)",       "Boolean"),
        new("ChildSeat",       "كُرسي أَطفال",              "Boolean"),
        new("Condition",       "حالَة المَركَبَة",          "SingleSelect", new[] { "new","excellent","good","fair" }),
        new("AvailableFrom",   "مُتاحَة اعتِباراً مَن",      "Date"),
    };

    /// <summary>مُناسَبات — صالات/كوش/ملابس عَرسان/تَجهيزات مَواليد.</summary>
    public static readonly IReadOnlyList<AttributeSeed> Events = new AttributeSeed[]
    {
        new("Capacity",        "السَّعَة (شَخص)",           "Number"),
        new("AreaSqm",         "المِساحَة (م²)",            "Number"),
        new("IndoorOutdoor",   "داخِلي/خارِجي",             "SingleSelect", new[] { "indoor","outdoor","both" }),
        new("EventTypes",      "أَنواع المُناسَبات",        "MultiSelect", new[] { "wedding","engagement","graduation","birthday","corporate","conference","funeral","other" }),
        new("AvailableFrom",   "مُتاحَة اعتِباراً مَن",      "Date"),
        new("SlotsPerDay",     "فَتَرات في اليَوم",         "SingleSelect", new[] { "morning","evening","both" }),
        new("DurationHours",   "مُدَّة الإيجار (ساعات)",     "Number"),
        new("DepositAmount",   "التَّأمين",                 "Number"),
        new("CancelPolicy",    "سِياسَة الإلغاء",           "SingleSelect", new[] { "flexible","moderate","strict","non_refundable" }),
        new("MinAdvanceDays",  "حَجز قَبل (يَوم)",          "Number"),
        new("Catering",        "خِدمَة الضِّيافَة",         "Boolean"),
        new("CateringIncluded","ضِيافَة مَشمولَة",          "Boolean"),
        new("ChairsTablesIncluded","كَراسي وَطاوِلات",      "Boolean"),
        new("Stage",           "مَنَصَّة",                   "Boolean"),
        new("Kosha",           "كوشَة",                    "Boolean"),
        new("Lighting",        "إنارَة احتِفالِيَّة",       "Boolean"),
        new("Sound",           "صَوتِيّات / DJ",            "Boolean"),
        new("Photography",     "تَصوير",                    "Boolean"),
        new("AirConditioning", "تَكييف",                    "Boolean"),
        new("ValetParking",    "خِدمَة صَفّ سَيّارات",      "Boolean"),
        new("WomenSection",    "قِسم لِلنِساء مُنفَصِل",     "Boolean"),
        new("PowerBackup",     "كَهرَباء احتِياطِيَّة",     "Boolean"),
        new("BridalRoom",      "غُرفَة عَروس",              "Boolean"),
    };

    /// <summary>مُخَيَّمات — لِلأَفراح والعائِلَة في اليَمَن.</summary>
    public static readonly IReadOnlyList<AttributeSeed> Camps = new AttributeSeed[]
    {
        new("Capacity",        "السَّعَة (شَخص)",           "Number"),
        new("AreaSqm",         "المِساحَة (م²)",            "Number"),
        new("Style",           "النَمَط",                   "SingleSelect", new[] { "modern","traditional","desert","mountain","beach" }),
        new("CampType",        "نَوع المُخَيَّم",            "SingleSelect", new[] { "weddings","family","tourism","corporate","mixed" }),
        new("TentCount",       "عَدَد الخِيام",              "Number"),
        new("RoomCount",       "عَدَد الغُرَف",              "Number"),
        new("BathroomCount",   "عَدَد الحَمّامات",           "Number"),
        new("PrivateBathroom", "حَمّام خاصّ بِالخَيمَة",     "Boolean"),
        new("HotWater",        "ماء ساخِن",                 "Boolean"),
        new("Kitchen",         "مَطبَخ",                    "Boolean"),
        new("Power",           "كَهرَباء / مُولِّد",         "Boolean"),
        new("Generator",       "مُولِّد كَهرَباء",           "Boolean"),
        new("SolarPower",      "طاقَة شَمسِيَّة",            "Boolean"),
        new("WaterTank",       "خَزّان ماء",                "Boolean"),
        new("Wifi",            "إنتَرنِت",                   "Boolean"),
        new("AirConditioning", "تَكييف",                    "Boolean"),
        new("Heating",         "تَدفِئَة",                  "Boolean"),
        new("FirePit",         "مَوقِد نار",                "Boolean"),
        new("Bbq",             "شَوّايَة",                  "Boolean"),
        new("PlayArea",        "مَلعَب أَطفال",              "Boolean"),
        new("Stage",           "مَنَصَّة",                   "Boolean"),
        new("SoundSystem",     "صَوتِيّات",                  "Boolean"),
        new("LightingShow",    "إنارَة احتِفالِيَّة",        "Boolean"),
        new("Catering",        "خِدمَة الضِّيافَة",          "Boolean"),
        new("StaffIncluded",   "طاقَم خِدمَة",               "Boolean"),
        new("HasMaintenance",  "تَنظيف وصِيانَة",            "Boolean"),
        new("WomenSection",    "قِسم لِلنِساء مُنفَصِل",     "Boolean"),
        new("PetsAllowed",     "يُسمَح بِالحَيَوانات",       "Boolean"),
        new("CancelPolicy",    "سِياسَة الإلغاء",            "SingleSelect", new[] { "flexible","moderate","strict","non_refundable" }),
        new("DepositAmount",   "التَّأمين",                  "Number"),
        new("MinAdvanceDays",  "حَجز قَبل (يَوم)",          "Number"),
        new("AvailableFrom",   "مُتاح اعتِباراً مَن",        "Date"),
    };

    /// <summary>يَختار قالَب الـ kind الصَّحيح مَن الـ slug. غَير مَعروف
    /// ⇒ <see cref="UniversalDefaults"/>.</summary>
    public static IReadOnlyList<AttributeSeed> ForKind(string kind) => kind?.ToLowerInvariant() switch
    {
        "residential" or "commercial" or "leisure" => Realty,
        "vehicles"                                 => Vehicle,
        "events"                                   => Events,
        "camps"                                    => Camps,
        _                                          => UniversalDefaults,
    };
}

/// <summary>POCO seed لِـ AttributeDefinition + خِيارات اختيارِيَّة.</summary>
public sealed record AttributeSeed(
    string Code, string Name, string Type,
    string[]? Options = null);
