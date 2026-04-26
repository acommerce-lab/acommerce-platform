namespace ACommerce.Authentication.Operations;

/// <summary>
/// Canonicalises user-entered phone numbers so a user who types "0501111111",
/// "966501111111", "00966501111111" and "+966501111111" all land on the same
/// record. Same applies to Yemen ("0771111111", "967771111111", "+967771111111").
/// Called by every AuthController before querying the user table.
///
/// Country auto-detection rules (single non-overlapping prefix per country):
///   leading-digit "5" + 9 digits  →  Saudi Arabia ("+9665...")
///   leading-digit "7" + 9 digits  →  Yemen        ("+9677...")
/// Add more entries to <see cref="LocalPrefixToCountry"/> when the platform
/// expands to other Arabic-speaking markets (e.g. UAE, Egypt, Iraq).
/// </summary>
public static class PhoneNormalization
{
    /// <summary>
    /// Local mobile leading digit → country dial code. Each entry must be a
    /// single digit unique to that country's mobile numbering plan, otherwise
    /// auto-detection becomes ambiguous and callers MUST pass an already-prefixed
    /// E.164 number.
    /// </summary>
    private static readonly Dictionary<char, string> LocalPrefixToCountry = new()
    {
        ['5'] = "966",  // Saudi Arabia
        ['7'] = "967",  // Yemen
    };

    /// <summary>
    /// Normalise to E.164: strip all non-digits, strip leading "00", prefix "+".
    /// If the remaining digits look like a local mobile number (9 digits or
    /// 10 digits with a leading "0"), prepend the appropriate country code per
    /// <see cref="LocalPrefixToCountry"/>.
    /// </summary>
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";
        // First convert Arabic-Indic (٠-٩) and Persian (۰-۹) digits to Latin
        // so `char.IsDigit` further down (which accepts Latin only for our
        // output) sees them as digits to keep.  Each code point maps 1-to-1:
        //   '٠' + n  →  '0' + n   (Arabic-Indic)
        //   '۰' + n  →  '0' + n   (Persian / Extended Arabic-Indic)
        var buf = new System.Text.StringBuilder(phone.Length);
        foreach (var ch in phone)
        {
            char mapped = ch;
            if (ch >= '٠' && ch <= '٩')       mapped = (char)('0' + (ch - '٠'));
            else if (ch >= '۰' && ch <= '۹')  mapped = (char)('0' + (ch - '۰'));
            buf.Append(mapped);
        }
        var normalized = buf.ToString();

        // Strip all non-Latin-digit characters (spaces, punctuation, '+', …).
        var digits = new string(normalized.Where(c => c >= '0' && c <= '9').ToArray());
        if (digits.StartsWith("00")) digits = digits[2..];

        // "0Xxxxxxxxxx" (10 digits, leading 0) → strip 0, prepend country code
        if (digits.Length == 10 && digits[0] == '0' &&
            LocalPrefixToCountry.TryGetValue(digits[1], out var ccLong))
            digits = ccLong + digits[1..];
        // "Xxxxxxxxxx"  (9 digits, no 0)       → prepend country code
        else if (digits.Length == 9 &&
                 LocalPrefixToCountry.TryGetValue(digits[0], out var ccShort))
            digits = ccShort + digits;

        return "+" + digits;
    }
}
