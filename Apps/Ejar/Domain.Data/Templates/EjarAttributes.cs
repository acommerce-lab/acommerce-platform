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

/// <summary>سِمات الإعلانات الديناميكِيَّة في Ejar — مَفاتيح ثابِتَة
/// لِكُلّ نَوع عَقار (PropertyType slug). الـ scope يُشتَقّ ديناميكيّاً
/// عَبر <see cref="EjarListingScopes.DeriveScopeId"/>.
///
/// <para>Defaults هُنا = سِمات عامَّة تُطَبَّق عَلى كُلّ الإعلانات.
/// لِجَعل سَمَة خاصَّة بِنَوع مُعَيَّن، يُضيف admin AttributeDefinition +
/// CategoryAttributeMapping بِـ CategoryId يَتَطابَق مَع
/// <c>DeriveScopeId("apartment")</c>.</para></summary>
public static class EjarListingAttributes
{
    public static readonly IReadOnlyList<AttributeSeed> Defaults = new AttributeSeed[]
    {
        new("Floor",        "الطابِق",         "SingleSelect", new[] { "ground", "first", "second", "third", "fourth", "fifth" }),
        new("Furnished",    "التَّأثيث",       "SingleSelect", new[] { "furnished", "unfurnished", "semi" }),
        new("Parking",      "المَواقِف",       "SingleSelect", new[] { "yes", "no", "covered" }),
        new("Elevator",     "مَصعَد",          "Boolean"),
        new("Balcony",      "شُرفَة",          "Boolean"),
    };
}

/// <summary>POCO seed لِـ AttributeDefinition + خِيارات اختيارِيَّة.</summary>
public sealed record AttributeSeed(
    string Code, string Name, string Type,
    string[]? Options = null);
