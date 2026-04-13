namespace Order.Web.Services;

/// <summary>
/// User-visible UI preferences (theme + language). Stored in-memory per
/// circuit; persisting them to localStorage is a one-line JS interop and
/// can be added later without touching the components that read them.
/// </summary>
public class UiPreferences
{
    public string Theme { get; private set; } = "light";   // "light" | "dark"
    public string Language { get; private set; } = "ar";   // "ar" | "en"

    public event Action? OnChanged;

    public bool IsDark => Theme == "dark";
    public bool IsArabic => Language == "ar";

    public void SetTheme(string theme)
    {
        if (theme != "light" && theme != "dark") return;
        Theme = theme;
        OnChanged?.Invoke();
    }

    public void ToggleTheme() => SetTheme(IsDark ? "light" : "dark");

    public void SetLanguage(string lang)
    {
        if (lang != "ar" && lang != "en") return;
        Language = lang;
        OnChanged?.Invoke();
    }

    public string Tr(string ar, string en) => IsArabic ? ar : en;
}
