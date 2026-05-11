using ACommerce.SharedKernel.Domain.DynamicAttributes;

namespace Ashare.V3.Data.Templates;

/// <summary>
/// قالَب سِمات البروفايل الكانوني لِـ V3. مَنفَصِل عَن
/// <see cref="V3CategoryTemplates"/> لِأَنّ البروفايل لَيس فِئَة إعلان —
/// إنَّه كِيان واحِد لِكُلّ تَطبيق. <c>Bootstrap</c> يُسَجِّله كَ row في
/// <c>CategoryAttributeTemplates</c> بِـ <c>CategorySlug = "profile"</c>
/// لِيُعاد استِخدام نَفس آلِيَّة DB-served + admin-lock.
///
/// <para><b>التَوسيع</b>: أَضِف حَقلاً هُنا، ارفَع <see cref="Version"/>،
/// أَعِد التَشغيل. Bootstrap يُحَدِّث DB row (إن لَم يَكُن مَقفولاً مِن
/// لوحَة التَحَكُّم). الواجِهَة تَلتَقِطه عَلى الفور.</para>
/// </summary>
public static class V3ProfileTemplate
{
    /// <summary>مِفتاح ثابِت يُمَيِّز هذا القالَب في DB.</summary>
    public const string Slug = "profile";

    public const int Version = 1;

    public static AttributeTemplate Build() => new()
    {
        Fields =
        {
            Select("user_type", "Account type",  "نوع الحساب",      "person-badge",     show: true,  order: 1, Types()),
            Select("language",  "Preferred lang","اللغة المُفَضَّلَة","translate",       show: false, order: 2, Languages()),
            Bool  ("notif_sms", "SMS alerts",    "تنبيهات SMS",     "chat-dots",        show: false, order: 3),
            Bool  ("notif_email","Email alerts", "تنبيهات بريد",    "envelope",         show: false, order: 4),
            Bool  ("public_phone","Show phone",  "إظهار رقم الجوال","telephone",        show: false, order: 5),
            Multi ("interests", "Interests",     "اهتمامات",        "stars",            show: true,  order: 6, Interests()),
            Num   ("years_exp", "Years of experience","سنوات الخبرة","clock-history",   show: false, order: 7),
        }
    };

    private static List<AttributeOption> Types() => new()
    {
        Opt("customer", "Customer", "مستأجر"),
        Opt("owner",    "Owner",    "مالك"),
        Opt("agent",    "Agent",    "وسيط"),
        Opt("company",  "Company",  "شركة"),
    };

    private static List<AttributeOption> Languages() => new()
    {
        Opt("ar", "Arabic",  "العربية"),
        Opt("en", "English", "الإنجليزية"),
    };

    private static List<AttributeOption> Interests() => new()
    {
        Opt("apartment",  "Apartments",  "شقق",     "building"),
        Opt("villa",      "Villas",      "فلل",     "house"),
        Opt("commercial", "Commercial",  "تجاري",   "shop"),
        Opt("land",       "Land",        "أراضٍ",   "tree"),
        Opt("short_term", "Short-term",  "إيجار قصير","calendar-event"),
    };

    private static AttributeFieldDefinition Bool(string k, string en, string ar, string icon, bool show, int order) =>
        new() { Key = k, Type = "bool",   Label = en, LabelAr = ar, Icon = icon, ShowInCard = show, SortOrder = order };
    private static AttributeFieldDefinition Num(string k, string en, string ar, string icon, bool show, int order) =>
        new() { Key = k, Type = "number", Label = en, LabelAr = ar, Icon = icon, ShowInCard = show, SortOrder = order };
    private static AttributeFieldDefinition Select(string k, string en, string ar, string icon, bool show, int order, List<AttributeOption> opts) =>
        new() { Key = k, Type = "select", Label = en, LabelAr = ar, Icon = icon, ShowInCard = show, SortOrder = order, Options = opts };
    private static AttributeFieldDefinition Multi(string k, string en, string ar, string icon, bool show, int order, List<AttributeOption> opts) =>
        new() { Key = k, Type = "multi",  Label = en, LabelAr = ar, Icon = icon, ShowInCard = show, SortOrder = order, Options = opts };
    private static AttributeOption Opt(string v, string en, string ar, string? icon = null) =>
        new() { Value = v, Label = en, LabelAr = ar, Icon = icon };
}
