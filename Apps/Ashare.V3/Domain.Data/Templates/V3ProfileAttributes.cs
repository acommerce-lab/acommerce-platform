namespace Ashare.V3.Data.Templates;

/// <summary>
/// مِفتاح ثابِت لِربط سِمات البروفايل في جَدول
/// <c>CategoryAttributeMappings</c> المَركَزي. النَّمَط نَفسه الَّذي
/// يَستَخدِمه أَسهَر لِفِئات المُنتَجات، لكِن بِـ "فِئَة" اصطِناعِيَّة
/// مُخَصَّصَة لِلبروفايل ⇒ نَستَفيد مِن نَفس مَحَرِّك القَوالِب بِلا
/// جَدول ديناميكي مُنفَصِل.
///
/// <para>الـ <c>AttributeDefinition.Code</c> لِكُلّ سِمَة بروفايل
/// يُطابِق مِفتاحَها في <c>Profile.AttributesJson</c>. القِيَم تُحفَظ
/// كَ JSON snapshot، لا كَأَعمِدَة عَلى Profile (الأَعمِدَة محدودة
/// بِواجِهَة <c>IUserProfile</c>).</para>
/// </summary>
public static class V3ProfileAttributes
{
    /// <summary>
    /// Sentinel <c>CategoryId</c> لِسِمات البروفايل. لا يُشير إلى صَفّ
    /// ProductCategory حَقيقي — مَجرَّد مِفتاح ثابِت لِـ
    /// <c>CategoryAttributeMappings.CategoryId</c>.
    /// </summary>
    public static readonly Guid CategoryId = new("00000000-0000-0000-0000-000000000P01".Replace("P", "F"));

    /// <summary>
    /// السِمات الديناميكِيَّة لِلبروفايل — كُلّ ما لا يَنتَمي لِواجِهَة
    /// <c>IUserProfile</c>. <c>UserId</c> و <c>NationalId</c> الوَحيدان
    /// الباقِيان كَأَعمِدَة (هَويَّة + Nafath lookup ⇒ أَداء)؛ كُلّ ما
    /// عَداهُما يُخَزَّن في <c>Profile.AttributesJson</c>.
    /// </summary>
    public static readonly IReadOnlyList<ProfileAttributeSeed> Defaults = new ProfileAttributeSeed[]
    {
        new("BusinessName",  "اسم النشاط التجاري", "Text"),
        new("Type",          "نوع الحساب",         "Number"),
        new("IsActive",      "نَشِط",              "Boolean"),
        new("IsVerified",    "مُوَثَّق (نَفاذ)",   "Boolean"),
        new("VerifiedAt",    "تاريخ التَوثيق",     "Date"),
        new("Address",       "العنوان",           "Text"),
        new("Country",       "الدولة",            "Text"),
        new("PostalCode",    "الرمز البريدي",     "Text"),
        new("Coordinates",   "الإحداثيات",        "Text"),
    };

    /// <param name="Code">مِفتاح في <c>Profile.AttributesJson</c>.</param>
    /// <param name="Name">العَرَبي المَعروض في الواجِهَة.</param>
    /// <param name="Type">enum name (Text/Number/Boolean/SingleSelect/…).</param>
    public sealed record ProfileAttributeSeed(string Code, string Name, string Type);
}
