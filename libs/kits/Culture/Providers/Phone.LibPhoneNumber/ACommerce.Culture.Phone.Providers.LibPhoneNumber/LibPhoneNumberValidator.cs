using ACommerce.Culture.Abstractions;
using System.Text.RegularExpressions;

namespace ACommerce.Culture.Phone.Providers.LibPhoneNumber;

/// <summary>
/// Wrapper intended to delegate to Google's libphonenumber-csharp for deep
/// validation (mobile vs. landline, region-specific length, carrier lookup).
/// Until the NuGet package is installed this falls back to E.164 regex +
/// a hard-coded table of country-code → (min,max) digit-count ranges.
/// Swap the implementation to `PhoneNumberUtil.GetInstance().IsValidNumber`
/// after adding the libphonenumber-csharp package reference.
/// </summary>
public sealed class LibPhoneNumberValidator : IPhoneNumberValidator
{
    private static readonly Regex E164 = new(@"^\+(\d{1,3})(\d{6,14})$", RegexOptions.Compiled);
    // country-code → (minNational, maxNational)
    private static readonly Dictionary<string, (int, int)> CountryLen = new()
    {
        ["966"] = (9, 9),   // Saudi Arabia (mobile starts with 5)
        ["971"] = (8, 9),   // UAE
        ["20"]  = (10, 10), // Egypt
        ["1"]   = (10, 10), // US/Canada
        ["44"]  = (10, 10), // UK
        ["33"]  = (9, 9),   // France
    };

    public bool IsValid(string e164Phone, string? defaultRegion = null)
    {
        if (string.IsNullOrWhiteSpace(e164Phone)) return false;
        var m = E164.Match(e164Phone);
        if (!m.Success) return false;
        var cc = m.Groups[1].Value;
        var national = m.Groups[2].Value;
        // find country code prefix (greedy match)
        foreach (var (code, (lo, hi)) in CountryLen.OrderByDescending(k => k.Key.Length))
        {
            if ((cc + national).StartsWith(code))
            {
                var len = (cc + national).Length - code.Length;
                return len >= lo && len <= hi;
            }
        }
        // Unknown country — accept if total length plausible.
        var total = cc.Length + national.Length;
        return total >= 8 && total <= 15;
    }
}
