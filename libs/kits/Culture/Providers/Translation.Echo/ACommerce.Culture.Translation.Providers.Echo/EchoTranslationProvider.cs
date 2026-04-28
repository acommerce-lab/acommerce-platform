using ACommerce.Culture.Translation.Abstractions;

namespace ACommerce.Culture.Translation.Providers.Echo;

/// <summary>
/// مُزوّد تطوير: يعيد النص كما هو مع وسم `[from→to]`.  يسمح بتفعيل خطّ
/// ترجمة في ASP.NET dependency injection دون الاعتماد على شبكة أو مفتاح.
/// </summary>
public sealed class EchoTranslationProvider : ITranslationProvider
{
    public string Name => "echo";

    public Task<string> TranslateAsync(string text, string fromLang, string toLang, CancellationToken ct = default)
        => Task.FromResult(fromLang == toLang ? text : $"[{fromLang}→{toLang}] {text}");

    public Task<IReadOnlyList<string>> TranslateBatchAsync(IReadOnlyList<string> texts, string fromLang, string toLang, CancellationToken ct = default)
    {
        var tagged = fromLang == toLang
            ? texts
            : texts.Select(t => $"[{fromLang}→{toLang}] {t}").ToList();
        return Task.FromResult<IReadOnlyList<string>>(tagged.ToList());
    }
}
