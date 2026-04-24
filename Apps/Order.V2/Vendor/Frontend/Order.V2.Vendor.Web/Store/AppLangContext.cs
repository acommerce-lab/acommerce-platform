using System.Globalization;
using System.Resources;
using ACommerce.L10n.Blazor;

namespace Order.V2.Vendor.Web.Store;

public sealed class AppLangContext : ILanguageContext
{
    private readonly AppStore _store;
    public AppLangContext(AppStore store) { _store = store; }
    public string Language => _store.Ui.Language;
    public bool IsRtl => _store.Ui.Language != "en";
}

/// <summary>
/// Reads translations from compiled .NET resource files
/// (<c>Resources/Strings.resx</c> — English default, <c>Resources/Strings.ar.resx</c> — Arabic).
/// Swapping back to embedded dictionaries or API-backed providers is a one-line DI change.
/// </summary>
public sealed class VendorTranslations : ITranslationProvider
{
    private static readonly ResourceManager _rm = new(
        "Order.V2.Vendor.Web.Resources.Strings",
        typeof(VendorTranslations).Assembly);

    public string Translate(string key, string language)
    {
        var culture = string.IsNullOrEmpty(language)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(language);
        return _rm.GetString(key, culture) ?? key;
    }
}
