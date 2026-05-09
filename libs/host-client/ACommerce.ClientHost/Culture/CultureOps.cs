using ACommerce.ClientHost.Preferences;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.ClientHost.Culture;

/// <summary>
/// مَصنَع عَمَليّات تَحَكُّم بِتَفضيلات الواجِهَة. كلّ op يَحوي
/// <c>ExecuteFunc</c> مَحَلّيّ يُحَدِّث <see cref="IUiPreferences"/> +
/// يُطلِق <c>NotifyChanged</c>. لا HTTP route، فيُشَغِّلها
/// <c>OpEngine.ExecuteAsync</c> مُباشَرَةً (لَيس <c>ClientOpEngine</c>).
/// post-interceptors المُسَجَّلة (telemetry/audit) تَلتَقِط بدون مَجهود.
/// </summary>
public static class CultureOps
{
    public static Operation SetLanguage(string language, IUiPreferences prefs) => Entry
        .Create("culture.set_language")
        .Describe($"User sets language to {language}")
        .From("User:current",     1, ("role", "user"))
        .To("UI:culture",         1, ("role", "preferences"))
        .Tag("language", language)
        .Execute(_ =>
        {
            prefs.Language = language;
            prefs.NotifyChanged();
            return Task.CompletedTask;
        })
        .Build();

    public static Operation SetTheme(string theme, IUiPreferences prefs) => Entry
        .Create("culture.set_theme")
        .Describe($"User sets theme to {theme}")
        .From("User:current",     1, ("role", "user"))
        .To("UI:preferences",     1, ("role", "preferences"))
        .Tag("theme", theme)
        .Execute(_ =>
        {
            prefs.Theme = theme;
            prefs.NotifyChanged();
            return Task.CompletedTask;
        })
        .Build();

    public static Operation SetCity(string city, IUiPreferences prefs) => Entry
        .Create("ui.set_city")
        .Describe($"User sets default city to {city}")
        .From("User:current",     1, ("role", "user"))
        .To("UI:preferences",     1, ("role", "preferences"))
        .Tag("city", city)
        .Execute(_ =>
        {
            prefs.City = city;
            prefs.NotifyChanged();
            return Task.CompletedTask;
        })
        .Build();
}
