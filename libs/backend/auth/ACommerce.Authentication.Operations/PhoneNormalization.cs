namespace ACommerce.Authentication.Operations;

/// <summary>
/// Canonicalises user-entered phone numbers so a user who types "0501111111",
/// "966501111111", "00966501111111" and "+966501111111" all land on the same
/// record.  Called by every AuthController before querying the user table.
/// </summary>
public static class PhoneNormalization
{
    /// <summary>
    /// Normalise to E.164: strip all non-digits, strip leading "00", prefix "+".
    /// If the remaining digits start with "5" and the app is a Saudi demo build
    /// (common seed phone "+9665..."), the country code "966" is prepended.
    /// </summary>
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";
        // First convert Arabic-Indic (٠-٩) and Persian (۰-۹) digits to Latin
        // so `char.IsDigit` further down (which accepts Latin only for our
        // output) sees them as digits to keep.  Each code point maps 1-to-1:
        //   '\u0660' + n  →  '0' + n   (Arabic-Indic)
        //   '\u06F0' + n  →  '0' + n   (Persian / Extended Arabic-Indic)
        var buf = new System.Text.StringBuilder(phone.Length);
        foreach (var ch in phone)
        {
            char mapped = ch;
            if (ch >= '\u0660' && ch <= '\u0669')       mapped = (char)('0' + (ch - '\u0660'));
            else if (ch >= '\u06F0' && ch <= '\u06F9')  mapped = (char)('0' + (ch - '\u06F0'));
            buf.Append(mapped);
        }
        var normalized = buf.ToString();

        // Strip all non-Latin-digit characters (spaces, punctuation, '+', …).
        var digits = new string(normalized.Where(c => c >= '0' && c <= '9').ToArray());
        if (digits.StartsWith("00")) digits = digits[2..];
        // "05xxxxxxxx" → "9665xxxxxxxx"
        if (digits.Length == 10 && digits.StartsWith("05")) digits = "966" + digits[1..];
        // "5xxxxxxxx"  → "9665xxxxxxxx"
        if (digits.Length == 9 && digits.StartsWith("5")) digits = "966" + digits;
        return "+" + digits;
    }
}
