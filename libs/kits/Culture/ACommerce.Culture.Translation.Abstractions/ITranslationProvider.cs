namespace ACommerce.Culture.Translation.Abstractions;

/// <summary>
/// مُزوّد ترجمة قابل للتركيب (Google, DeepL, LLM, Echo).
/// أي تطبيق يعتمد ITranslationProvider فقط — والتركيب في DI يختار المُزوّد.
/// </summary>
public interface ITranslationProvider
{
    /// <summary>اسم المزوّد للتشخيص ("echo", "google", "ai").</summary>
    string Name { get; }

    /// <summary>يترجم نصاً واحداً من اللغة المصدر إلى اللغة الهدف.</summary>
    Task<string> TranslateAsync(string text, string fromLang, string toLang,
                                CancellationToken ct = default);

    /// <summary>يترجم دفعة من النصوص معاً (أكثر كفاءة).</summary>
    Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> texts, string fromLang, string toLang,
        CancellationToken ct = default);
}
