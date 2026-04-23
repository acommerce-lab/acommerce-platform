using ACommerce.L10n.Blazor;

namespace Order.V2.Web.Store;

public sealed class AppLangContext : ILanguageContext
{
    private readonly AppStore _store;
    public AppLangContext(AppStore store) { _store = store; }
    public string Language => _store.Ui.Language;
    public bool IsRtl => _store.Ui.Language != "en";
}

public sealed class CustomerTranslations : EmbeddedTranslationProvider
{
    protected override IReadOnlyDictionary<string, string> Ar => _ar;
    protected override IReadOnlyDictionary<string, string> En => _en;
    private static readonly Dictionary<string, string> _ar = new();
    private static readonly Dictionary<string, string> _en = new();
}
