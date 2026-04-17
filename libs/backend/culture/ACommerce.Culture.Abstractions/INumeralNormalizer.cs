namespace ACommerce.Culture.Abstractions;

/// <summary>
/// يحوّل الأرقام بين أنظمة الأرقام (لاتينية، هندية ٠١٢٣٤٥٦٧٨٩، فارسية ۰۱۲۳۴۵۶۷۸۹، إلخ).
/// الخلفيّة تُخزّن كل شيء بلاتيني — هذا المطبع يُستخدم:
///   • كمعترض قبل الحفظ (تحويل مدخلات المستخدم إلى لاتيني)
///   • عند العرض (لتقديم الأرقام بنظام المستخدم المفضّل)
/// </summary>
public interface INumeralNormalizer
{
    /// <summary>يحوّل كل الأرقام في النص إلى اللاتينية 0-9 (آمن للبحث والتخزين).</summary>
    string ToLatin(string? input);

    /// <summary>يحوّل الأرقام اللاتينية إلى النظام المحدد ("arabic-indic" / "persian" / "latin").</summary>
    string FromLatin(string? input, string targetSystem);
}
