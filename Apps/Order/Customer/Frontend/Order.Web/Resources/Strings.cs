using System.Resources;
using System.Globalization;

namespace Order.Web.Resources;

/// <summary>
/// Auto-generated class for accessing localized strings from .resx files.
/// Resource files: Strings.resx (en), Strings.ar.resx (ar)
/// </summary>
public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new ResourceManager("Order.Web.Resources.Strings", typeof(Strings).Assembly);

    /// <summary>
    /// Get string resource by key using specified culture.
    /// </summary>
    public static string GetString(string key, CultureInfo? culture = null)
    {
        return ResourceManager.GetString(key, culture) ?? key;
    }

    // Navigation strings
    public static string nav_brand => GetString(nameof(nav_brand));
    public static string nav_home => GetString(nameof(nav_home));
    public static string nav_search => GetString(nameof(nav_search));
    public static string nav_orders => GetString(nameof(nav_orders));
    public static string nav_messages => GetString(nameof(nav_messages));
    public static string nav_cart => GetString(nameof(nav_cart));
    public static string nav_profile => GetString(nameof(nav_profile));
    public static string nav_signin => GetString(nameof(nav_signin));

    // Home page strings
    public static string home_title => GetString(nameof(home_title));
    public static string home_subtitle => GetString(nameof(home_subtitle));
    public static string home_all => GetString(nameof(home_all));
    public static string home_loading => GetString(nameof(home_loading));
    public static string home_empty => GetString(nameof(home_empty));
    public static string home_signin => GetString(nameof(home_signin));
    public static string home_language_toggle => GetString(nameof(home_language_toggle));

    // Settings page strings
    public static string settings_title => GetString(nameof(settings_title));
    public static string settings_theme => GetString(nameof(settings_theme));
    public static string settings_theme_light => GetString(nameof(settings_theme_light));
    public static string settings_theme_dark => GetString(nameof(settings_theme_dark));
    public static string settings_language => GetString(nameof(settings_language));
    public static string settings_language_ar => GetString(nameof(settings_language_ar));
    public static string settings_language_en => GetString(nameof(settings_language_en));
    public static string settings_about => GetString(nameof(settings_about));
    public static string settings_version => GetString(nameof(settings_version));
    public static string settings_sign_out => GetString(nameof(settings_sign_out));
    public static string settings_terms => GetString(nameof(settings_terms));

    // Common strings
    public static string app_name => GetString(nameof(app_name));
    public static string common_loading => GetString(nameof(common_loading));
    public static string common_error => GetString(nameof(common_error));
}
