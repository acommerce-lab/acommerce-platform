using System.Globalization;
using System.Resources;
using ACommerce.L10n.Blazor;

namespace Ejar.Customer.UI.Store;

/// <summary>
/// تَنفيذ <see cref="ITranslationProvider"/> (مِن L10n.Blazor) يَقرأ مِن
/// مَلَفّات الـ resx المُرفَقَة في القالَب: <c>Resources/Strings.resx</c>
/// (English) + <c>Resources/Strings.ar.resx</c> (Arabic).
/// </summary>
public sealed class EjarTranslationProvider : ITranslationProvider
{
    private static readonly ResourceManager _rm = new(
        "Ejar.Customer.UI.Resources.Strings",
        typeof(EjarTranslationProvider).Assembly);

    public string Translate(string key, string language)
    {
        var culture = string.IsNullOrEmpty(language)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(language);
        return _rm.GetString(key, culture) ?? key;
    }
}
