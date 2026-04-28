using System.Text.RegularExpressions;
using ACommerce.Culture.Abstractions;

namespace ACommerce.Culture.Defaults;

/// <summary>
/// التحقّق الأساسي: رقم E.164 (بين 8 و15 رقماً بعد +).
/// يُطبَّق على الأرقام الموحَّدة عبر PhoneNormalization.Normalize.
/// لفحص أدقّ (مثل التأكّد أنه رقم جوّال فعلاً لبلد معين)، استخدم
/// ACommerce.Culture.Phone.Providers.LibPhoneNumber.
/// </summary>
public sealed class RegexPhoneNumberValidator : IPhoneNumberValidator
{
    // E.164: +[1-9][0-9]{7,14}
    private static readonly Regex E164 = new(@"^\+[1-9]\d{7,14}$", RegexOptions.Compiled);

    public bool IsValid(string e164Phone, string? defaultRegion = null)
        => !string.IsNullOrWhiteSpace(e164Phone) && E164.IsMatch(e164Phone);
}
