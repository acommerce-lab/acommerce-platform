using System.Globalization;
using System.Resources;
using ACommerce.L10n.Blazor;

namespace Order.V2.Web.Store;

/// <summary>
/// Razor façade — delegates to <see cref="ITranslationProvider"/>.
/// إبقاء نفس صيغة الاستخدام (<c>L["order.title"]</c>) للصفحات:
///   @inject L L
///   <h1>@(L["order.title"])</h1>
/// </summary>
//public class L
//{
//    private readonly AppStore _store;
//    private readonly ITranslationProvider _provider;

//    public L(AppStore store, ITranslationProvider provider)
//    {
//        _store = store;
//        _provider = provider;
//    }

//    public string this[string key] => _provider.Translate(key, _store.Ui.Language);

//    public bool IsRtl => _store.Ui.IsRtl;
//    public string Lang => _store.Ui.Language;
//}

/// <summary>
/// Reads translations from compiled .NET resource files
/// (<c>Resources/Strings.resx</c> — English default, <c>Resources/Strings.ar.resx</c> — Arabic).
/// Swapping back to embedded dictionaries or API-backed providers is a one-line DI change.
/// </summary>
public sealed class CustomerTranslations : ITranslationProvider
{
    private static readonly ResourceManager _rm = new(
        "Order.V2.Web.Resources.Strings",
        typeof(CustomerTranslations).Assembly);

    /// <summary>عَقد <see cref="ITranslationProvider"/>: يَرُدّ <c>null</c>
    /// لَو المِفتاح غَير مَوجود — يُتيح لِـ LayeredTranslationProvider
    /// تَخطّي هذه الطَّبَقَة لِلطَّبَقَة التالِيَة.</summary>
    public string? TryTranslate(string key, string language)
    {
        var culture = string.IsNullOrEmpty(language)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(language);
        return _rm.GetString(key, culture);
    }

    public string Translate(string key, string language)
        => TryTranslate(key, language) ?? key;
}
