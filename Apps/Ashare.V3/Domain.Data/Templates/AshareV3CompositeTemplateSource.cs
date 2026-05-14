using ACommerce.Kits.DynamicAttributes.Backend;
using ACommerce.SharedKernel.Domain.DynamicAttributes;

namespace Ashare.V3.Data.Templates;

/// <summary>
/// مَصدَر قَوالِب مُرَكَّب لِـ V3:
/// <list type="number">
///   <item>سِمات بروفايل sentinel ⇒ يُفَوَّض لِـ ProductionAttributeTemplateSource.</item>
///   <item>سِمات روممَت (<c>roommate_has</c>/<c>roommate_wants</c>) ⇒
///         قالَب hardcoded — لا تَوجَد في جَدول الإنتاج.</item>
///   <item>أَيّ scope آخَر (CategoryId إنتاج فِعلي) ⇒ يُفَوَّض لِـ
///         ProductionAttributeTemplateSource.</item>
/// </list>
///
/// <para>الـ scopeId المُشتَقّ مَن slug "roommate_has"/"roommate_wants"
/// يَتَطابَق مَع Guids في <see cref="AshareV3TaxonomyStore"/>.</para>
/// </summary>
public sealed class AshareV3CompositeTemplateSource : IAttributeTemplateSource
{
    private readonly ProductionAttributeTemplateSource _prod;
    public AshareV3CompositeTemplateSource(ProductionAttributeTemplateSource prod) => _prod = prod;

    // الـ Guids ثابِتَة — نَفسها في <see cref="AshareV3TaxonomyStore"/>.
    private static readonly Guid RoommateHas   = Guid.Parse("0a01a01a-0a01-0a01-0a01-0a01000a01a2");
    private static readonly Guid RoommateWants = Guid.Parse("0a01a01a-0a01-0a01-0a01-0a01000a01a3");

    public Task<AttributeTemplate?> BuildForScopeAsync(Guid scopeId, CancellationToken ct)
    {
        if (scopeId == RoommateHas)
            return Task.FromResult<AttributeTemplate?>(RoommateHasTemplate);
        if (scopeId == RoommateWants)
            return Task.FromResult<AttributeTemplate?>(RoommateWantsTemplate);

        // كُلّ ما عَدا ذلك ⇒ الإنتاج (asharedb).
        return _prod.BuildForScopeAsync(scopeId, ct);
    }

    // ─── أَدوات بِناء حُقول مُختَصَرَة ───────────────────────────────
    // الـ DSL في الأَسفَل يَجعَل القَوالِب المُطَوَّلَة قابِلَة لِلقِراءَة:
    // F("Key", "تَسمِيَة", "number")    ⇒ حَقل رَقَم
    // S("Key", "تَسمِيَة", (val,ar)…)   ⇒ حَقل خِيار مَع تَرجَمات عَرَبِيَّة
    private static AttributeFieldDefinition F(string key, string ar, string type) =>
        new() { Key = key, Label = ar, LabelAr = ar, Type = type };

    private static AttributeFieldDefinition S(string key, string ar, params (string Value, string LabelAr)[] opts) =>
        new()
        {
            Key = key, Label = ar, LabelAr = ar, Type = "select",
            Options = opts.Select(o => new AttributeOption
            {
                Value = o.Value, Label = o.Value, LabelAr = o.LabelAr
            }).ToList(),
        };

    private static AttributeFieldDefinition M(string key, string ar, params (string Value, string LabelAr)[] opts) =>
        new()
        {
            Key = key, Label = ar, LabelAr = ar, Type = "multi",
            Options = opts.Select(o => new AttributeOption
            {
                Value = o.Value, Label = o.Value, LabelAr = o.LabelAr
            }).ToList(),
        };

    private static AttributeTemplate Template(params AttributeFieldDefinition[] fields)
    {
        for (int i = 0; i < fields.Length; i++) fields[i].SortOrder = i + 1;
        return new AttributeTemplate { Fields = fields.ToList() };
    }

    // ─── قَوالِب الروممَت ─────────────────────────────────────────────
    // "عَنده سَكَن" — يَنشُر غُرفَة/مَكاناً في شَقَّة، فَيَحتاج وَصف
    // الإيجار + المُتَطَلَّبات في الشَريك. كُلّ الحُقول اختِيارِيَّة
    // مَن مَنظور الـ wizard — صاحِب الإعلان يَملَأ ما يَستَطيع.
    private static readonly AttributeTemplate RoommateHasTemplate = Template(
        // ─ تَفاصيل المَكان ─
        F("RoomPrice",          "سِعر الغُرفَة الشَّهري (ريال)",  "number"),
        S("BedroomShare",       "نَوع الغُرفَة",
            ("private",   "خاصَّة"),
            ("shared",    "مُشتَرَكَة")),
        F("RoomCount",          "عَدَد الغُرَف في الشَقَّة",       "number"),
        F("BathroomCount",      "عَدَد الحَمّامات",                "number"),
        F("PrivateBathroom",    "حَمّام خاصّ بِالغُرفَة",          "bool"),
        F("RoommatesPresent",   "عَدَد الرُّفَقاء الحالِيّين",     "number"),
        S("PropertyType",       "نَوع السَكَن",
            ("apartment", "شَقَّة"),
            ("villa",     "فيلا"),
            ("studio",    "اِستوديو"),
            ("room",      "غُرفَة مُستَقِلَّة")),
        F("Floor",              "الطابِق",                        "text"),
        F("AreaSqm",            "مِساحَة الشَقَّة (م²)",          "number"),
        F("AvailableFrom",      "مُتاحَة اعتِباراً مَن",           "date"),
        S("MinimumStay",        "أَقَلّ مُدَّة سُكنى",
            ("monthly",   "شَهر"),
            ("3months",   "ثَلاثَة أَشهُر"),
            ("6months",   "سِتَّة أَشهُر"),
            ("yearly",    "سَنَة")),
        F("DepositAmount",      "التَّأمين",                      "number"),
        S("UtilitiesIncluded",  "الفَواتير مَشمولَة",
            ("all",         "الكُلّ مَشمول"),
            ("partial",     "بَعضها مَشمول"),
            ("none",        "غَير مَشمولَة")),
        F("Furnished",          "مُؤَثَّثَة",                      "bool"),
        F("Wifi",               "إنتَرنِت",                       "bool"),
        F("AirConditioning",    "تَكييف",                          "bool"),
        F("HotWater",           "ماء ساخِن",                      "bool"),
        F("Kitchen",            "مَطبَخ مُشتَرَك",                "bool"),
        F("Laundry",            "غَسّالَة مَلابِس",                "bool"),
        F("Parking",            "مَوقِف سَيّارَة",                "bool"),
        F("Elevator",           "مَصعَد",                          "bool"),
        F("PowerBackup",        "كَهرَباء احتِياطِيَّة",           "bool"),
        F("WaterTank",          "خَزّان ماء",                      "bool"),
        // ─ مَواصَفات الشَّريك المَطلوب ─
        S("GenderPref",         "تَفضيل الجِنس",
            ("male",   "ذَكَر"),
            ("female", "أُنثى"),
            ("any",    "أَيّ")),
        F("MinAgePref",         "أَدنى عُمر مَطلوب",               "number"),
        F("MaxAgePref",         "أَقصى عُمر مَطلوب",               "number"),
        M("OccupationPref",     "مِهنَة الشَّريك المُفَضَّلَة",
            ("student",    "طالِب"),
            ("employee",   "مُوَظَّف"),
            ("freelancer", "عَمَل حُرّ"),
            ("any",        "أَيّ")),
        S("Smoking",            "التَدخين",
            ("allowed",     "مَسموح"),
            ("outdoor_only","في الخارِج فَقَط"),
            ("not_allowed", "غَير مَسموح")),
        S("Pets",               "حَيَوانات أَليفَة",
            ("allowed",     "مَسموح"),
            ("not_allowed", "غَير مَسموح"),
            ("case_by_case","حَسب الحالَة")),
        S("Cleanliness",        "مُستَوى النَّظافَة المَطلوب",
            ("very_clean","عالِيَة جِدّاً"),
            ("clean",     "جَيِّدَة"),
            ("relaxed",   "عادِيَّة")),
        S("Lifestyle",          "نَمَط الحَياة",
            ("quiet",       "هادِئ"),
            ("social",      "اجتِماعي"),
            ("party",       "حَيَوي")),
        S("VisitorsPolicy",     "سِياسَة الزُّوّار",
            ("allowed",     "مَسموح"),
            ("limited",     "مَحدود"),
            ("not_allowed", "غَير مَسموح")),
        S("Religion",           "الدِّيانَة",
            ("any",      "غَير مُهِمّ"),
            ("muslim",   "مُسلِم"),
            ("other",    "أُخرى")),
        F("RoommateBio",        "وَصف الجَوّ العام (نَبذَة)",       "text")
    );

    // "يَدور سَكَن" — مَعلومات الباحِث وَ مُتَطَلَّباته.
    private static readonly AttributeTemplate RoommateWantsTemplate = Template(
        // ─ مَن أَنا ─
        F("Age",                "العُمر",                          "number"),
        S("Gender",             "الجِنس",
            ("male",   "ذَكَر"),
            ("female", "أُنثى")),
        F("Occupation",         "المِهنَة",                        "text"),
        S("MaritalStatus",      "الحالَة الاجتِماعِيَّة",
            ("single",   "أَعزَب"),
            ("married",  "مُتَزَوِّج"),
            ("divorced", "مُطَلَّق"),
            ("other",    "أُخرى")),
        F("Nationality",        "الجِنسِيَّة",                     "text"),
        F("Languages",          "اللُغات",                          "text"),
        F("AboutMe",            "نَبذَة عَنّي",                    "text"),
        S("Religion",           "الدِّيانَة",
            ("any",      "غَير مُهِمّ"),
            ("muslim",   "مُسلِم"),
            ("other",    "أُخرى")),
        // ─ ما أَبحَث عَنه ─
        F("Budget",             "المِيزانِيَّة الشَّهرِيَّة (ريال)","number"),
        F("PreferredArea",      "المَنطِقَة المُفَضَّلَة",           "text"),
        M("PreferredCities",    "المُدُن المُفَضَّلَة",
            ("sanaa",      "صَنعاء"),
            ("aden",       "عَدَن"),
            ("taiz",       "تَعِزّ"),
            ("hudaydah",   "الحُدَيدَة"),
            ("ibb",        "إب"),
            ("hadramout",  "حَضرَموت")),
        S("PreferredPropertyType","نَوع السَكَن المُفَضَّل",
            ("apartment", "شَقَّة"),
            ("villa",     "فيلا"),
            ("studio",    "اِستوديو"),
            ("room",      "غُرفَة مُستَقِلَّة")),
        S("BedroomShare",       "نَوع الغُرفَة المَطلوبَة",
            ("private",   "خاصَّة"),
            ("shared",    "مُشتَرَكَة"),
            ("any",       "أَيّ")),
        S("PrivateBathroom",    "حَمّام خاصّ مَطلوب",
            ("required",  "ضَروري"),
            ("preferred", "مُفَضَّل"),
            ("any",       "غَير ضَروري")),
        S("FurnishedPref",      "التَّأثيث المُفَضَّل",
            ("furnished",   "مُؤَثَّث"),
            ("unfurnished", "غَير مُؤَثَّث"),
            ("any",         "أَيّ")),
        F("MoveInBy",           "تاريخ الانتِقال المَطلوب",         "date"),
        S("StayDuration",       "مُدَّة الإقامَة المُتَوَقَّعَة",
            ("short",   "أَقَلّ مَن ٣ أَشهُر"),
            ("medium",  "٣ — ٦ أَشهُر"),
            ("long",    "سَنَة أَو أَكثَر")),
        // ─ نَمَط حَياتي ─
        S("Smoker",             "هَل أُدَخِّن",
            ("yes_outside",  "نَعَم خارِج المَنزِل"),
            ("yes",          "نَعَم"),
            ("no",           "لا")),
        F("HasPet",             "أَملِك حَيَوان أَليف",             "bool"),
        S("Lifestyle",          "نَمَط حَياتي",
            ("quiet",   "هادِئ"),
            ("social",  "اجتِماعي"),
            ("flexible","مَرِن")),
        S("Cleanliness",        "مُستَوى النَّظافَة",
            ("very_clean","عالِيَة جِدّاً"),
            ("clean",     "جَيِّدَة"),
            ("relaxed",   "عادِيَّة")),
        S("VisitorsPolicy",     "زُوّاري المُتَوَقَّعون",
            ("rarely",   "نادِراً"),
            ("sometimes","أَحياناً"),
            ("often",    "كَثيراً")),
        // ─ تَفضيلات الشَّريك ─
        S("RoommateGenderPref", "جِنس الشَّريك المُفَضَّل",
            ("male", "ذَكَر"),
            ("female","أُنثى"),
            ("any",  "أَيّ")),
        F("RoommateMinAge",     "أَدنى عُمر لِلشَّريك",             "number"),
        F("RoommateMaxAge",     "أَقصى عُمر لِلشَّريك",             "number"),
        S("RoommateCountPref",  "عَدَد الرُّفَقاء المُفَضَّل",
            ("solo",       "بِمُفرَدي مَع مالِك"),
            ("one",        "شَريك واحِد"),
            ("two_plus",   "اِثنان فَأَكثَر"),
            ("any",        "أَيّ"))
    );
}
