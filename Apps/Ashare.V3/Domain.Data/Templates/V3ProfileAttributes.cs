namespace Ashare.V3.Data.Templates;

/// <summary>
/// مِفتاح ثابِت لِربط سِمات البروفايل في جَدول
/// <c>CategoryAttributeMappings</c> المَركَزي. النَّمَط نَفسه الَّذي
/// يَستَخدِمه أَسهَر لِفِئات المُنتَجات، لكِن بِـ "فِئَة" اصطِناعِيَّة
/// مُخَصَّصَة لِلبروفايل ⇒ نَستَفيد مِن نَفس مَحَرِّك القَوالِب بِلا
/// جَدول ديناميكي مُنفَصِل.
///
/// <para>الـ <see cref="AttributeDefinition.Code"/> لِكُلّ سِمَة بروفايل
/// يُطابِق <b>اسم خاصِّيَّة <see cref="Ashare.V3.Domain.ProfileEntity"/> بِالضَّبط</b>
/// (NationalId, BusinessName, Address, Country, PostalCode, Coordinates).
/// الـ <c>ProfileAttributesController</c> يَستَخدِم reflection لِنَقل
/// القِيَم مِن/إلى الأَعمِدَة.</para>
/// </summary>
public static class V3ProfileAttributes
{
    /// <summary>
    /// Sentinel <c>CategoryId</c> لِسِمات البروفايل. لا يُشير إلى صَفّ
    /// ProductCategory حَقيقي — مَجرَّد مِفتاح ثابِت لِـ
    /// <c>CategoryAttributeMappings.CategoryId</c>.
    /// </summary>
    public static readonly Guid CategoryId = new("00000000-0000-0000-0000-000000000P01".Replace("P", "F"));

    /// <summary>سِمات يَنشَأها <c>SeedAsync</c> إن لَم تَكُن مَوجودَة في DB.</summary>
    public static readonly IReadOnlyList<ProfileAttributeSeed> Defaults = new ProfileAttributeSeed[]
    {
        new("NationalId",    "رقم الهوية",        "Text"),
        new("BusinessName",  "اسم النشاط التجاري", "Text"),
        new("Address",       "العنوان",           "Text"),
        new("Country",       "الدولة",            "Text"),
        new("PostalCode",    "الرمز البريدي",     "Text"),
        new("Coordinates",   "الإحداثيات",        "Text"),
    };

    /// <param name="Code">يُطابِق اسم property في ProfileEntity (reflection).</param>
    /// <param name="Name">العَرَبي المَعروض في الواجِهَة.</param>
    /// <param name="Type">enum name (Text/Number/Boolean/SingleSelect/…).</param>
    public sealed record ProfileAttributeSeed(string Code, string Name, string Type);
}
