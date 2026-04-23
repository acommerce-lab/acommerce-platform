using System.Globalization;
using System.Resources;
using Order.Web.Resources;
using Order.Web.Store;

namespace Order.Web.Services;

/// <summary>
/// Localization service using .NET Resource Files (.resx)
/// Supports multiple languages and automatic culture switching.
/// </summary>
public class LocalizationService
{
    private readonly AppStore _store;
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    public event EventHandler? LanguageChanged;

    public LocalizationService(AppStore store)
    {
        _store = store;
        _resourceManager = new ResourceManager(
            typeof(Strings));
        _currentCulture = GetCultureFromLanguage(_store.Ui.Language);

        // Subscribe to language changes
        _store.OnChanged += OnStoreChanged;
    }

    /// <summary>
    /// Get localized string by key from resource file.
    /// </summary>
    public string GetString(string key)
    {
        return _resourceManager.GetString(key, _currentCulture) ?? key;
    }

    /// <summary>
    /// Get localized string with fallback to key if not found.
    /// </summary>
    public string this[string key] => GetString(key);

    /// <summary>
    /// Current language code (en, ar, etc.).
    /// </summary>
    public string Language => _store.Ui.Language;

    /// <summary>
    /// Current culture info.
    /// </summary>
    public CultureInfo CurrentCulture => _currentCulture;

    /// <summary>
    /// Get all available language codes and their display names.
    /// </summary>
    public IReadOnlyDictionary<string, string> AvailableLanguages => new Dictionary<string, string>
    {
        { "en", "English" },
        { "ar", "العربيّة" }
    };

    private void OnStoreChanged()
    {
        if (_store.Ui.Language != Language)
        {
            _currentCulture = GetCultureFromLanguage(_store.Ui.Language);
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static CultureInfo GetCultureFromLanguage(string language)
    {
        return language switch
        {
            "ar" => new CultureInfo("ar"),
            "en" => new CultureInfo("en"),
            _ => new CultureInfo("en")
        };
    }

    /// <summary>
    /// Initialize localization with language from store.
    /// Call this in app startup.
    /// </summary>
    public void Initialize()
    {
        _currentCulture = GetCultureFromLanguage(_store.Ui.Language);
        CultureInfo.CurrentUICulture = _currentCulture;
    }
}
