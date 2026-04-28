using ACommerce.Culture.Translation.Abstractions;
using System.Net.Http.Json;
using System.Text.Json;

namespace ACommerce.Culture.Translation.Providers.Google;

public sealed class GoogleTranslateOptions
{
    public string ApiKey { get; init; } = "";
    public string Endpoint { get; init; } = "https://translation.googleapis.com/language/translate/v2";
}

/// <summary>
/// Google Cloud Translate v2.  يتطلّب مفتاح API (يُمرَّر عبر GoogleTranslateOptions).
/// HttpClient يُحقَن — استخدم AddHttpClient&lt;GoogleTranslationProvider&gt;() في DI.
/// </summary>
public sealed class GoogleTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _http;
    private readonly GoogleTranslateOptions _opts;

    public GoogleTranslationProvider(HttpClient http, GoogleTranslateOptions opts)
    { _http = http; _opts = opts; }

    public string Name => "google";

    public async Task<string> TranslateAsync(string text, string fromLang, string toLang, CancellationToken ct = default)
    {
        var batch = await TranslateBatchAsync(new[] { text }, fromLang, toLang, ct);
        return batch[0];
    }

    public async Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> texts, string fromLang, string toLang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
            throw new InvalidOperationException("GoogleTranslate: API key not configured.");
        if (texts.Count == 0) return Array.Empty<string>();

        var url = $"{_opts.Endpoint}?key={Uri.EscapeDataString(_opts.ApiKey)}";
        var req = new { q = texts, source = fromLang, target = toLang, format = "text" };
        using var res = await _http.PostAsJsonAsync(url, req, ct);
        res.EnsureSuccessStatusCode();
        using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var arr = doc.RootElement.GetProperty("data").GetProperty("translations");
        var result = new List<string>(arr.GetArrayLength());
        foreach (var el in arr.EnumerateArray())
            result.Add(el.GetProperty("translatedText").GetString() ?? "");
        return result;
    }
}
