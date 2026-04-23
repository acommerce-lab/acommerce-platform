using System.Globalization;
using System.Resources;
using Order.Web.Resources;
using Order.Web.Store;

namespace Order.Web.Services;

/// <summary>
/// Localization service using .NET Resource Files (.resx).
/// Binds to AppStore.Ui.Language changes and exposes strongly-typed
/// access through Strings properties or indexer by key.
/// Supports multi-language (en, ar, and any culture with a .resx).
/// </summary>
public class LocalizationService
{
    private readonly AppStore _store;
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;
    private string _lastLanguage;

    public event EventHandler? LanguageChanged;

    public LocalizationService(AppStore store)
    {
        _store = store;
        _resourceManager = new ResourceManager(typeof(Strings));
        _lastLanguage = _store.Ui.Language;
        _currentCulture = GetCultureFromLanguage(_lastLanguage);
        CultureInfo.CurrentUICulture = _currentCulture;

        _store.OnChanged += OnStoreChanged;
    }

    /// <summary>Get localized string by resource key.</summary>
    public string GetString(string key)
    {
        return _resourceManager.GetString(key, _currentCulture) ?? key;
    }

    /// <summary>Indexer access for Razor: @Localization["key"]</summary>
    public string this[string key] => GetString(key);

    public string Language => _lastLanguage;
    public CultureInfo CurrentCulture => _currentCulture;
    public bool IsRtl => _currentCulture.TextInfo.IsRightToLeft;

    /// <summary>Languages available with display names.</summary>
    public IReadOnlyDictionary<string, string> AvailableLanguages => new Dictionary<string, string>
    {
        { "en", "English" },
        { "ar", "العربيّة" }
    };

    private void OnStoreChanged()
    {
        var newLang = _store.Ui.Language;
        if (newLang != _lastLanguage)
        {
            _lastLanguage = newLang;
            _currentCulture = GetCultureFromLanguage(newLang);
            CultureInfo.CurrentUICulture = _currentCulture;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static CultureInfo GetCultureFromLanguage(string language)
    {
        try { return CultureInfo.GetCultureInfo(language); }
        catch { return CultureInfo.GetCultureInfo("en"); }
    }
}
