using System.Globalization;
using System.Resources;

namespace Ejar.Web.Store;

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
/// Reads translations from compiled .NET resource files
/// (<c>Resources/Strings.resx</c> — English default, <c>Resources/Strings.ar.resx</c> — Arabic).
/// Name kept as <c>EmbeddedTranslationProvider</c> for DI continuity;
/// swap the backing store by registering a different ITranslationProvider.
/// </summary>
public sealed class EmbeddedTranslationProvider : ITranslationProvider
{
    private static readonly ResourceManager _rm = new(
        "Ejar.Web.Resources.Strings",
        typeof(EmbeddedTranslationProvider).Assembly);

    public string Translate(string key, string language)
    {
        var culture = string.IsNullOrEmpty(language)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(language);
        return _rm.GetString(key, culture) ?? key;
    }
}
