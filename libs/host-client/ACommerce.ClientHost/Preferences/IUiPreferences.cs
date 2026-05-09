namespace ACommerce.ClientHost.Preferences;

/// <summary>
/// تَفضيلات الواجِهة لِلمُستَخدِم — الأَشياء التي تَتَخَزَّن لكلّ
/// device/جَلسة لكنّها ليسَت auth ولا culture. مَثَل:
/// <list type="bullet">
///   <item>Theme (light/dark)</item>
///   <item>City/Region الافتراضيّ</item>
///   <item>HideChrome flag (vendors/embed)</item>
///   <item>Recent searches</item>
///   <item>Active quick filters</item>
///   <item>UI-only flags (sidebar collapsed، …)</item>
/// </list>
///
/// <para>يَتَزامَن مَع localStorage مِثل <see cref="Auth.IClientAuthState"/>.
/// التَطبيق يُنفِّذ نَوعاً مَخصَّصاً (Ejar's UiState) أَو يَستَخدِم
/// <see cref="DefaultUiPreferences"/> الافتراضيّ.</para>
/// </summary>
public interface IUiPreferences
{
    string  Theme    { get; set; }
    /// <summary>
    /// لُغَة الواجِهَة (ISO-639: "ar" / "en"). Setter لا يُحَدِّث ICultureContext
    /// مُباشَرَةً — التَطبيق يَدفَع <c>CultureOps.SetLanguage(...)</c> عَبر
    /// <c>IClientCultureController</c> أَو ما يُعادِله، فيَستَدعي هذا الـ
    /// setter داخِل ExecuteFunc الـ op (لِيَحفَظ التَتَبُّع OAM).
    /// </summary>
    string  Language { get; set; }
    string  City     { get; set; }
    bool    HideChrome { get; set; }
    IList<string> RecentSearches { get; }
    IList<string> ActiveQuickFilterIds { get; }

    event Action? OnChanged;
    void NotifyChanged();
}

/// <summary>تَنفيذ افتراضيّ POCO لـ <see cref="IUiPreferences"/>.</summary>
public sealed class DefaultUiPreferences : IUiPreferences
{
    public string Theme      { get; set; } = "light";
    public string Language   { get; set; } = "ar";
    public string City       { get; set; } = "";
    public bool   HideChrome { get; set; }
    public IList<string> RecentSearches       { get; } = new List<string>();
    public IList<string> ActiveQuickFilterIds { get; } = new List<string>();

    public event Action? OnChanged;
    public void NotifyChanged() => OnChanged?.Invoke();
}
