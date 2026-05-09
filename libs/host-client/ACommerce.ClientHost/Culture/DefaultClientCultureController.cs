using ACommerce.ClientHost.Preferences;
using ACommerce.OperationEngine.Core;

namespace ACommerce.ClientHost.Culture;

/// <summary>
/// تَنفيذ افتراضيّ لِـ <see cref="IClientCultureController"/> — يَدفَع كلّ
/// تَفضيل عَبر <see cref="OpEngine.ExecuteAsync"/> مُباشَرَةً. ExecuteFunc
/// المَبنيّ في <see cref="CultureOps"/> يَقوم بِالتَحديث المَحَلّيّ فَيُلتَقِط
/// <see cref="LocalStorageUiPersistence"/> التَغيير عَبر
/// <c>IUiPreferences.OnChanged</c> ⇒ يَحفَظ في localStorage تلقائيّاً.
/// </summary>
public sealed class DefaultClientCultureController : IClientCultureController
{
    private readonly OpEngine _engine;
    private readonly IUiPreferences _prefs;

    public DefaultClientCultureController(OpEngine engine, IUiPreferences prefs)
    {
        _engine = engine;
        _prefs  = prefs;
    }

    public Task SetLanguageAsync(string language, CancellationToken ct = default) =>
        _engine.ExecuteAsync(CultureOps.SetLanguage(language, _prefs), ct);

    public Task SetThemeAsync(string theme, CancellationToken ct = default) =>
        _engine.ExecuteAsync(CultureOps.SetTheme(theme, _prefs), ct);

    public Task SetCityAsync(string city, CancellationToken ct = default) =>
        _engine.ExecuteAsync(CultureOps.SetCity(city, _prefs), ct);
}
