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
        new("BedroomCount",  "عَدَد الغُرَف",       "Number"),
        new("BathroomCount", "عَدَد الحَمّامات",     "Number"),
        new("AreaSqm",       "المِساحَة (م²)",        "Number"),
        new("Floor",         "الطابِق",              "SingleSelect", new[] { "ground","first","second","third","fourth","fifth","sixth","seventh","eighth","ninth","tenth" }),
        new("Furnished",     "التَّأثيث",            "SingleSelect", new[] { "furnished","unfurnished","semi" }),
        new("Parking",       "المَواقِف",            "SingleSelect", new[] { "yes","no","covered" }),
        new("Elevator",      "مَصعَد",               "Boolean"),
        new("Balcony",       "شُرفَة",                "Boolean"),
    };

    /// <summary>مَركَبات — سَيّارات/باصات/درّاجات/دَينات.</summary>
    public static readonly IReadOnlyList<AttributeSeed> Vehicle = new AttributeSeed[]
    {
        new("Make",          "الصُنع",                "Text"),
        new("Model",         "المُوديل",              "Text"),
        new("Year",          "السَنَة",               "Number"),
        new("Mileage",       "العَدّاد (كم)",         "Number"),
        new("FuelType",      "الوَقود",               "SingleSelect", new[] { "petrol","diesel","electric","hybrid" }),
        new("Transmission",  "ناقِل الحَرَكَة",       "SingleSelect", new[] { "automatic","manual" }),
        new("Capacity",      "عَدَد الرُكّاب",        "Number"),
        new("HasDriver",     "مَع سائِق",            "Boolean"),
        new("AirConditioning","تَكييف",              "Boolean"),
    };

    /// <summary>مُناسَبات — صالات/كوش/ملابس عَرسان/تَجهيزات مَواليد.</summary>
    public static readonly IReadOnlyList<AttributeSeed> Events = new AttributeSeed[]
    {
        new("Capacity",      "السَّعَة",              "Number"),
        new("IndoorOutdoor", "داخِلي/خارِجي",         "SingleSelect", new[] { "indoor","outdoor","both" }),
        new("Catering",      "ضِيافَة",              "Boolean"),
        new("Stage",         "مَنَصَّة",              "Boolean"),
        new("Sound",         "صَوتِيّات",             "Boolean"),
    };

    /// <summary>مُخَيَّمات — لِلأَفراح والعائِلَة في اليَمَن.</summary>
    public static readonly IReadOnlyList<AttributeSeed> Camps = new AttributeSeed[]
    {
        new("Capacity",      "السَّعَة",              "Number"),
        new("Power",         "كَهرَباء/مُولِّد",      "Boolean"),
        new("WaterTank",     "خَزّان ماء",           "Boolean"),
        new("HasMaintenance","تَنظيف وصِيانَة",       "Boolean"),
        new("Style",         "النَمَط",              "SingleSelect", new[] { "modern","traditional" }),
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
