namespace ACommerce.L10n.Blazor;

/// <summary>
/// واجهة الاستخدام في الصفحات: <c>@inject L L</c> ثم <c>@(L["home.title"])</c>.
/// تقرأ اللغة من <see cref="ILanguageContext"/> وتفوّض إلى <see cref="ITranslationProvider"/>.
/// </summary>
public class L
{
    private readonly ILanguageContext _ctx;
    private readonly ITranslationProvider _provider;

    public L(ILanguageContext ctx, ITranslationProvider provider)
    {
        _ctx = ctx;
        _provider = provider;
    }

    public string this[string key] => _provider.Translate(key, _ctx.Language);

    public string Lang  => _ctx.Language;
    public bool   IsRtl => _ctx.IsRtl;

    /// <summary>اختصار شرطي: <c>L.T(arabicText, englishText)</c>.</summary>
    public string T(string ar, string en) => _ctx.Language == "en" ? en : ar;
}
