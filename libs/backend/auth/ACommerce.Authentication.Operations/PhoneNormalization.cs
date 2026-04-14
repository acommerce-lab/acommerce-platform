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
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("00")) digits = digits[2..];
        // "05xxxxxxxx" → "9665xxxxxxxx"
        if (digits.Length == 10 && digits.StartsWith("05")) digits = "966" + digits[1..];
        // "5xxxxxxxx"  → "9665xxxxxxxx"
        if (digits.Length == 9 && digits.StartsWith("5")) digits = "966" + digits;
        return "+" + digits;
    }
}
