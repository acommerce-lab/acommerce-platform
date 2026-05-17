namespace Ashare.V3.Data.Templates;

/// <summary>
/// مَصدَر بَيانات وَحيد لِفِئَتَي الـ roommate في V3 + سِماتهما الديناميكِيَّة.
///
/// <para><b>لِماذا في مَكان مُستَقِلّ</b>: نَفس البَيانات تَخدِم
/// مَسارَين:
/// <list type="number">
///   <item><b>Seed في DB</b>: <see cref="Bootstrap.AshareV3Bootstrap"/>
///         يَزرَع <c>ProductCategories</c> + <c>AttributeDefinitions</c> +
///         <c>AttributeValues</c> + <c>CategoryAttributeMappings</c> مَن هذه
///         القائِمَة. هذا يَمنَح لوحَة الإدارَة قُدرَة التَّعديل (الـ
///         in-memory مُجَمَّد في الكود ولا يَقبَل تَعديلاً عَبر الواجِهَة).</item>
///   <item><b>Fallback in-memory</b>: <see cref="AshareV3CompositeTemplateSource"/>
///         يَبني نَفس الـ <c>AttributeTemplate</c> مِن هذه القائِمَة لَو
///         الـ seed لَم يَركَض (مَثَلاً: bootstrap حَدَّى خَطَأ، أَو
///         أَوَّل تَشغيل قَبل migration).</item>
/// </list></para>
///
/// <para><b>قاعِدَة المَصدَر الوَحيد</b>: لا تَنسَخ هذه القَوالِب إلى
/// مَكان آخَر. أَيّ تَوسيع لِسِمَة جَديدَة يَتِمّ هُنا، ثُمّ يَنعَكِس
/// في DB عِندَ إعادَة الـ bootstrap.</para>
/// </summary>
public static class AshareV3RoommateAttributes
{
    // الـ Guids ثابِتَة عَبر تَشغيلات مُتَعَدِّدَة — أَيّ تَغيير هُنا يَكسِر
    // الرَّبط مَع ProductListings المَوجودَة الَّتي تُشير إلى نَفس CategoryId.
    public static readonly Guid RoommateHasCategoryId   = Guid.Parse("0a01a01a-0a01-0a01-0a01-0a01000a01a2");
    public static readonly Guid RoommateWantsCategoryId = Guid.Parse("0a01a01a-0a01-0a01-0a01-0a01000a01a3");

    public const string RoommateHasSlug   = "roommate_has";
    public const string RoommateWantsSlug = "roommate_wants";

    public const string RoommateHasName   = "عشير عنده سكن";
    public const string RoommateWantsName = "عشير يدور سكن";

    /// <summary>وَصف حَقل سِمَة: <c>Code</c>, <c>Name</c> (عَرَبي عادَةً),
    /// <c>Type</c> (يُطابِق enum <c>AttributeType</c> في asharedb:
    /// SingleSelect/MultiSelect/Number/Text/LongText/Boolean/Date/DateTime).
    /// لِأَنواع select-like، <c>Options</c> يَحوي قائِمَة قِيَم.</summary>
    public sealed record AttrSeed(string Code, string Name, string Type, OptSeed[]? Options = null);

    /// <summary>خِيار قائِمَة: <c>Value</c> هو الـ key المَخزون، <c>LabelAr</c>
    /// التَّسمِيَة المَعروضَة عَرَبيّاً.</summary>
    public sealed record OptSeed(string Value, string LabelAr);

    // ─── سِمات "عَنده سَكَن" ──────────────────────────────────────────
    // المالِك يَنشُر غُرفَة/مَكاناً، فَالحُقول تَصِف الإيجار
    // ومُتَطَلَّبات الشَّريك. كُلّها اختِيارِيَّة في الـ wizard.
    public static readonly IReadOnlyList<AttrSeed> RoommateHasFields = new AttrSeed[]
    {
        // ─ تَفاصيل المَكان ─
        new("RoomPrice",          "سِعر الغُرفَة الشَّهري (ريال)",  "Number"),
        new("BedroomShare",       "نَوع الغُرفَة",                   "SingleSelect", new[]
        {
            new OptSeed("private", "خاصَّة"),
            new OptSeed("shared",  "مُشتَرَكَة"),
        }),
        new("RoomCount",          "عَدَد الغُرَف في الشَقَّة",       "Number"),
        new("BathroomCount",      "عَدَد الحَمّامات",                "Number"),
        new("PrivateBathroom",    "حَمّام خاصّ بِالغُرفَة",          "Boolean"),
        new("RoommatesPresent",   "عَدَد الرُّفَقاء الحالِيّين",     "Number"),
        new("PropertyType",       "نَوع السَكَن",                    "SingleSelect", new[]
        {
            new OptSeed("apartment", "شَقَّة"),
            new OptSeed("villa",     "فيلا"),
            new OptSeed("studio",    "اِستوديو"),
            new OptSeed("room",      "غُرفَة مُستَقِلَّة"),
        }),
        new("Floor",              "الطابِق",                         "Text"),
        new("AreaSqm",            "مِساحَة الشَقَّة (م²)",           "Number"),
        new("AvailableFrom",      "مُتاحَة اعتِباراً مَن",            "Date"),
        new("MinimumStay",        "أَقَلّ مُدَّة سُكنى",              "SingleSelect", new[]
        {
            new OptSeed("monthly",  "شَهر"),
            new OptSeed("3months",  "ثَلاثَة أَشهُر"),
            new OptSeed("6months",  "سِتَّة أَشهُر"),
            new OptSeed("yearly",   "سَنَة"),
        }),
        new("DepositAmount",      "التَّأمين",                       "Number"),
        new("UtilitiesIncluded",  "الفَواتير مَشمولَة",              "SingleSelect", new[]
        {
            new OptSeed("all",      "الكُلّ مَشمول"),
            new OptSeed("partial",  "بَعضها مَشمول"),
            new OptSeed("none",     "غَير مَشمولَة"),
        }),
        new("Furnished",          "مُؤَثَّثَة",                       "Boolean"),
        new("Wifi",               "إنتَرنِت",                         "Boolean"),
        new("AirConditioning",    "تَكييف",                           "Boolean"),
        new("HotWater",           "ماء ساخِن",                        "Boolean"),
        new("Kitchen",            "مَطبَخ مُشتَرَك",                  "Boolean"),
        new("Laundry",            "غَسّالَة مَلابِس",                 "Boolean"),
        new("Parking",            "مَوقِف سَيّارَة",                  "Boolean"),
        new("Elevator",           "مَصعَد",                           "Boolean"),
        new("PowerBackup",        "كَهرَباء احتِياطِيَّة",            "Boolean"),
        new("WaterTank",          "خَزّان ماء",                       "Boolean"),
        // ─ مَواصَفات الشَّريك المَطلوب ─
        new("GenderPref",         "تَفضيل الجِنس",                   "SingleSelect", new[]
        {
            new OptSeed("male",   "ذَكَر"),
            new OptSeed("female", "أُنثى"),
            new OptSeed("any",    "أَيّ"),
        }),
        new("MinAgePref",         "أَدنى عُمر مَطلوب",                "Number"),
        new("MaxAgePref",         "أَقصى عُمر مَطلوب",                "Number"),
        new("OccupationPref",     "مِهنَة الشَّريك المُفَضَّلَة",     "MultiSelect", new[]
        {
            new OptSeed("student",    "طالِب"),
            new OptSeed("employee",   "مُوَظَّف"),
            new OptSeed("freelancer", "عَمَل حُرّ"),
            new OptSeed("any",        "أَيّ"),
        }),
        new("Smoking",            "التَدخين",                        "SingleSelect", new[]
        {
            new OptSeed("allowed",      "مَسموح"),
            new OptSeed("outdoor_only", "في الخارِج فَقَط"),
            new OptSeed("not_allowed",  "غَير مَسموح"),
        }),
        new("Pets",               "حَيَوانات أَليفَة",                "SingleSelect", new[]
        {
            new OptSeed("allowed",      "مَسموح"),
            new OptSeed("not_allowed",  "غَير مَسموح"),
            new OptSeed("case_by_case", "حَسب الحالَة"),
        }),
        new("Cleanliness",        "مُستَوى النَّظافَة المَطلوب",     "SingleSelect", new[]
        {
            new OptSeed("very_clean", "عالِيَة جِدّاً"),
            new OptSeed("clean",      "جَيِّدَة"),
            new OptSeed("relaxed",    "عادِيَّة"),
        }),
        new("Lifestyle",          "نَمَط الحَياة",                   "SingleSelect", new[]
        {
            new OptSeed("quiet",   "هادِئ"),
            new OptSeed("social",  "اجتِماعي"),
            new OptSeed("party",   "حَيَوي"),
        }),
        new("VisitorsPolicy",     "سِياسَة الزُّوّار",                "SingleSelect", new[]
        {
            new OptSeed("allowed",     "مَسموح"),
            new OptSeed("limited",     "مَحدود"),
            new OptSeed("not_allowed", "غَير مَسموح"),
        }),
        new("Religion",           "الدِّيانَة",                       "SingleSelect", new[]
        {
            new OptSeed("any",    "غَير مُهِمّ"),
            new OptSeed("muslim", "مُسلِم"),
            new OptSeed("other",  "أُخرى"),
        }),
        new("RoommateBio",        "وَصف الجَوّ العام (نَبذَة)",       "LongText"),
    };

    // ─── سِمات "يَدور سَكَن" ───────────────────────────────────────────
    // الباحِث يَصِف نَفسَه وَ ما يَطلُبه.
    public static readonly IReadOnlyList<AttrSeed> RoommateWantsFields = new AttrSeed[]
    {
        // ─ مَن أَنا ─
        new("Age",                "العُمر",                          "Number"),
        new("Gender",             "الجِنس",                          "SingleSelect", new[]
        {
            new OptSeed("male",   "ذَكَر"),
            new OptSeed("female", "أُنثى"),
        }),
        new("Occupation",         "المِهنَة",                        "Text"),
        new("MaritalStatus",      "الحالَة الاجتِماعِيَّة",          "SingleSelect", new[]
        {
            new OptSeed("single",   "أَعزَب"),
            new OptSeed("married",  "مُتَزَوِّج"),
            new OptSeed("divorced", "مُطَلَّق"),
            new OptSeed("other",    "أُخرى"),
        }),
        new("Nationality",        "الجِنسِيَّة",                     "Text"),
        new("Languages",          "اللُغات",                         "Text"),
        new("AboutMe",            "نَبذَة عَنّي",                    "LongText"),
        new("Religion",           "الدِّيانَة",                      "SingleSelect", new[]
        {
            new OptSeed("any",    "غَير مُهِمّ"),
            new OptSeed("muslim", "مُسلِم"),
            new OptSeed("other",  "أُخرى"),
        }),
        // ─ ما أَبحَث عَنه ─
        new("Budget",             "المِيزانِيَّة الشَّهرِيَّة (ريال)","Number"),
        new("PreferredArea",      "المَنطِقَة المُفَضَّلَة",          "Text"),
        new("PreferredCities",    "المُدُن المُفَضَّلَة",             "MultiSelect", new[]
        {
            new OptSeed("riyadh",       "الرياض"),
            new OptSeed("jeddah",       "جدة"),
            new OptSeed("makkah",       "مكة المكرمة"),
            new OptSeed("madinah",      "المدينة المنورة"),
            new OptSeed("dammam",       "الدمام"),
            new OptSeed("khobar",       "الخبر"),
            new OptSeed("dhahran",      "الظهران"),
            new OptSeed("taif",         "الطائف"),
            new OptSeed("buraidah",     "بريدة"),
            new OptSeed("tabuk",        "تبوك"),
            new OptSeed("hail",         "حائل"),
            new OptSeed("abha",         "أبها"),
            new OptSeed("khamis",       "خميس مشيط"),
            new OptSeed("najran",       "نجران"),
            new OptSeed("jazan",        "جازان"),
            new OptSeed("yanbu",        "ينبع"),
        }),
        new("PreferredPropertyType","نَوع السَكَن المُفَضَّل",        "SingleSelect", new[]
        {
            new OptSeed("apartment", "شَقَّة"),
            new OptSeed("villa",     "فيلا"),
            new OptSeed("studio",    "اِستوديو"),
            new OptSeed("room",      "غُرفَة مُستَقِلَّة"),
        }),
        new("BedroomShare",       "نَوع الغُرفَة المَطلوبَة",         "SingleSelect", new[]
        {
            new OptSeed("private", "خاصَّة"),
            new OptSeed("shared",  "مُشتَرَكَة"),
            new OptSeed("any",     "أَيّ"),
        }),
        new("PrivateBathroom",    "حَمّام خاصّ مَطلوب",               "SingleSelect", new[]
        {
            new OptSeed("required",  "ضَروري"),
            new OptSeed("preferred", "مُفَضَّل"),
            new OptSeed("any",       "غَير ضَروري"),
        }),
        new("FurnishedPref",      "التَّأثيث المُفَضَّل",             "SingleSelect", new[]
        {
            new OptSeed("furnished",   "مُؤَثَّث"),
            new OptSeed("unfurnished", "غَير مُؤَثَّث"),
            new OptSeed("any",         "أَيّ"),
        }),
        new("MoveInBy",           "تاريخ الانتِقال المَطلوب",         "Date"),
        new("StayDuration",       "مُدَّة الإقامَة المُتَوَقَّعَة",   "SingleSelect", new[]
        {
            new OptSeed("short",  "أَقَلّ مَن ٣ أَشهُر"),
            new OptSeed("medium", "٣ — ٦ أَشهُر"),
            new OptSeed("long",   "سَنَة أَو أَكثَر"),
        }),
        // ─ نَمَط حَياتي ─
        new("Smoker",             "هَل أُدَخِّن",                     "SingleSelect", new[]
        {
            new OptSeed("yes_outside", "نَعَم خارِج المَنزِل"),
            new OptSeed("yes",         "نَعَم"),
            new OptSeed("no",          "لا"),
        }),
        new("HasPet",             "أَملِك حَيَوان أَليف",              "Boolean"),
        new("Lifestyle",          "نَمَط حَياتي",                    "SingleSelect", new[]
        {
            new OptSeed("quiet",    "هادِئ"),
            new OptSeed("social",   "اجتِماعي"),
            new OptSeed("flexible", "مَرِن"),
        }),
        new("Cleanliness",        "مُستَوى النَّظافَة",              "SingleSelect", new[]
        {
            new OptSeed("very_clean", "عالِيَة جِدّاً"),
            new OptSeed("clean",      "جَيِّدَة"),
            new OptSeed("relaxed",    "عادِيَّة"),
        }),
        new("VisitorsPolicy",     "زُوّاري المُتَوَقَّعون",          "SingleSelect", new[]
        {
            new OptSeed("rarely",    "نادِراً"),
            new OptSeed("sometimes", "أَحياناً"),
            new OptSeed("often",     "كَثيراً"),
        }),
        // ─ تَفضيلات الشَّريك ─
        new("RoommateGenderPref", "جِنس الشَّريك المُفَضَّل",         "SingleSelect", new[]
        {
            new OptSeed("male",   "ذَكَر"),
            new OptSeed("female", "أُنثى"),
            new OptSeed("any",    "أَيّ"),
        }),
        new("RoommateMinAge",     "أَدنى عُمر لِلشَّريك",             "Number"),
        new("RoommateMaxAge",     "أَقصى عُمر لِلشَّريك",             "Number"),
        new("RoommateCountPref",  "عَدَد الرُّفَقاء المُفَضَّل",      "SingleSelect", new[]
        {
            new OptSeed("solo",     "بِمُفرَدي مَع مالِك"),
            new OptSeed("one",      "شَريك واحِد"),
            new OptSeed("two_plus", "اِثنان فَأَكثَر"),
            new OptSeed("any",      "أَيّ"),
        }),
    };
}
