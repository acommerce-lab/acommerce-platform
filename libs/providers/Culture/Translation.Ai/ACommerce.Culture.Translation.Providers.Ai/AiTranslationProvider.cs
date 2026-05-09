using ACommerce.Culture.Translation.Abstractions;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ACommerce.Culture.Translation.Providers.Ai;

public sealed class AiTranslateOptions
{
    /// <summary>"anthropic" | "openai". حاليّاً يدعم anthropic كافتراضي.</summary>
    public string Vendor { get; init; } = "anthropic";
    public string ApiKey { get; init; } = "";
    public string Model  { get; init; } = "claude-haiku-4-5-20251001";
    public string AnthropicEndpoint { get; init; } = "https://api.anthropic.com/v1/messages";
    public string OpenAiEndpoint    { get; init; } = "https://api.openai.com/v1/chat/completions";
}

/// <summary>
/// يطلب من نموذج ذكاء اصطناعي الترجمة.  أكثر مرونة من Google للمحتوى الطويل
/// أو الثقافات الأقل دعماً، لكن أبطأ وأغلى.
/// </summary>
public sealed class AiTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _http;
    private readonly AiTranslateOptions _opts;

    public AiTranslationProvider(HttpClient http, AiTranslateOptions opts)
    { _http = http; _opts = opts; }

    public string Name => $"ai:{_opts.Vendor}";

    public async Task<string> TranslateAsync(string text, string fromLang, string toLang, CancellationToken ct = default)
    {
        if (fromLang == toLang) return text;
        var prompt = $"Translate the following text from {fromLang} to {toLang}. " +
                     "Output ONLY the translation, no prelude, no quotes. Preserve formatting.\n\n" + text;
        return _opts.Vendor switch
        {
            "openai" => await CallOpenAiAsync(prompt, ct),
            _        => await CallAnthropicAsync(prompt, ct)
        };
    }

    public async Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> texts, string fromLang, string toLang, CancellationToken ct = default)
    {
        // Simple batch: one call per item.  A smarter impl would concatenate with
        // separators — left as an optimisation.
        var result = new string[texts.Count];
        for (int i = 0; i < texts.Count; i++)
            result[i] = await TranslateAsync(texts[i], fromLang, toLang, ct);
        return result;
    }

    private async Task<string> CallAnthropicAsync(string prompt, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_opts.ApiKey))
            throw new InvalidOperationException("AiTranslate(anthropic): API key not configured.");
        var req = new HttpRequestMessage(HttpMethod.Post, _opts.AnthropicEndpoint)
        {
            Content = JsonContent.Create(new
            {
                model = _opts.Model,
                max_tokens = 2048,
                messages = new[] { new { role = "user", content = prompt } }
            })
        };
        req.Headers.Add("x-api-key", _opts.ApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
    }

    private async Task<string> CallOpenAiAsync(string prompt, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_opts.ApiKey))
            throw new InvalidOperationException("AiTranslate(openai): API key not configured.");
        var req = new HttpRequestMessage(HttpMethod.Post, _opts.OpenAiEndpoint)
        {
            Content = JsonContent.Create(new
            {
                model = _opts.Model,
                messages = new[] { new { role = "user", content = prompt } }
            })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.GetProperty("choices")[0]
            .GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}
