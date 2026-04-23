namespace ACommerce.L10n.Blazor;

/// <summary>
/// ProviderContract: مصدر الترجمات.
/// يسمح بتبديل المصدر (Embedded → CMS → Resx → API) بلا لمس أي صفحة.
/// </summary>
public interface ITranslationProvider
{
    /// <summary>يرجع النصّ للمفتاح حسب اللغة. إن لم يُوجد يرجع المفتاح نفسه.</summary>
    string Translate(string key, string language);
}
