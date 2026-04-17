using ACommerce.Culture.Abstractions;
using System.Text;

namespace ACommerce.Culture.Defaults;

/// <summary>
/// يحوّل الأرقام الهندية (٠-٩) والفارسية (۰-۹) إلى اللاتينية وبالعكس.
/// مُحسَّن: لا يستخدم regex — مسح حرف-بحرف مع استبدال نقطي.
/// </summary>
public sealed class DefaultNumeralNormalizer : INumeralNormalizer
{
    // أرقام هندية-عربية (Arabic-Indic):   ٠ ١ ٢ ٣ ٤ ٥ ٦ ٧ ٨ ٩
    // U+0660..U+0669
    // أرقام فارسية (Extended Arabic-Indic):  ۰ ۱ ۲ ۳ ۴ ۵ ۶ ۷ ۸ ۹
    // U+06F0..U+06F9

    public string ToLatin(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;
        var changed = false;
        var buf = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (ch >= '\u0660' && ch <= '\u0669')       // Arabic-Indic
            { buf.Append((char)('0' + (ch - '\u0660'))); changed = true; }
            else if (ch >= '\u06F0' && ch <= '\u06F9')  // Persian
            { buf.Append((char)('0' + (ch - '\u06F0'))); changed = true; }
            else
            { buf.Append(ch); }
        }
        return changed ? buf.ToString() : input;
    }

    public string FromLatin(string? input, string targetSystem)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;
        var offset = targetSystem switch
        {
            "arabic-indic" => 0x0660 - '0',
            "persian"      => 0x06F0 - '0',
            _              => 0
        };
        if (offset == 0) return input;
        var buf = new StringBuilder(input.Length);
        foreach (var ch in input)
            buf.Append(ch >= '0' && ch <= '9' ? (char)(ch + offset) : ch);
        return buf.ToString();
    }
}
