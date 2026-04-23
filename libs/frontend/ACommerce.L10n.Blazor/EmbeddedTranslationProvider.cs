namespace ACommerce.L10n.Blazor;

/// <summary>
/// Base قابل للإرث: كل تطبيق يوفّر قاموسين ثابتين <c>Ar</c> و<c>En</c>.
/// الاستخدام:
/// <code>
/// public sealed class VendorTranslations : EmbeddedTranslationProvider
/// {
///     protected override IReadOnlyDictionary&lt;string, string&gt; Ar =&gt; _ar;
///     protected override IReadOnlyDictionary&lt;string, string&gt; En =&gt; _en;
///     private static readonly Dictionary&lt;string, string&gt; _ar = new() { ["nav.home"] = "الرئيسية" };
///     private static readonly Dictionary&lt;string, string&gt; _en = new() { ["nav.home"] = "Home" };
/// }
/// </code>
/// </summary>
public abstract class EmbeddedTranslationProvider : ITranslationProvider
{
    protected abstract IReadOnlyDictionary<string, string> Ar { get; }
    protected abstract IReadOnlyDictionary<string, string> En { get; }

    public virtual string Translate(string key, string language)
    {
        if (language == "en" && En.TryGetValue(key, out var en)) return en;
        return Ar.TryGetValue(key, out var ar) ? ar : key;
    }
}
