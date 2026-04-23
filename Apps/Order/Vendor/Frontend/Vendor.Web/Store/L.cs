namespace Vendor.Web.Store;

/// <summary>
/// Translation service interface — implement to swap translation providers
/// (e.g. EmbeddedTranslationProvider, ApiTranslationProvider, ResxTranslationProvider).
/// </summary>
public interface ITranslationProvider
{
    string Translate(string key, string language);
}

/// <summary>
/// Razor façade — delegates to <see cref="ITranslationProvider"/>.
/// Keep the same format (@inject L L) for pages: @(L["vendor.title"])
/// </summary>
public class L
{
    private readonly AppStore _store;
    private readonly ITranslationProvider _provider;

    public L(AppStore store, ITranslationProvider provider)
    {
        _store = store;
        _provider = provider;
    }

    public string this[string key] => _provider.Translate(key, _store.Ui.Language);

    public bool IsRtl => _store.Ui.IsRtl;
    public string Lang => _store.Ui.Language;
}

/// <summary>
/// Embedded translation dictionaries — replace later with ApiTranslationProvider
/// or ResxTranslationProvider by changing DI registration only.
/// </summary>
public sealed class EmbeddedTranslationProvider : ITranslationProvider
{
    public string Translate(string key, string language)
    {
        if (language == "en" && En.TryGetValue(key, out var en)) return en;
        return Ar.TryGetValue(key, out var ar) ? ar : key;
    }

    private static readonly Dictionary<string, string> Ar = new()
    {
        ["app.name"] = "لوحة التاجر",

        ["nav.brand"] = "لوحة التاجر",
        ["nav.home"] = "الرئيسية",
        ["nav.orders"] = "الطلبات",
        ["nav.offers"] = "العروض",
        ["nav.messages"] = "الرسائل",
        ["nav.profile"] = "حسابي",
        ["nav.signin"] = "دخول",

        ["home.title"] = "الرئيسية",
        ["home.welcome"] = "أهلاً بك في لوحة التاجر",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["app.name"] = "Vendor",

        ["nav.brand"] = "Vendor",
        ["nav.home"] = "Home",
        ["nav.orders"] = "Orders",
        ["nav.offers"] = "Offers",
        ["nav.messages"] = "Messages",
        ["nav.profile"] = "Profile",
        ["nav.signin"] = "Sign in",

        ["home.title"] = "Home",
        ["home.welcome"] = "Welcome to Vendor Dashboard",
    };
}
