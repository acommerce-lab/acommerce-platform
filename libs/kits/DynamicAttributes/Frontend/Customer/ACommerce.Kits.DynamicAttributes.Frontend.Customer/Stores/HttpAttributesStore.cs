using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using ACommerce.SharedKernel.Domain.DynamicAttributes;

namespace ACommerce.Kits.DynamicAttributes.Frontend.Customer.Stores;

/// <summary>
/// تَنفيذ افتِراضي لِـ <see cref="IAttributesStore"/> يَستَخدِم
/// <c>HttpClient</c> المَحقون لِلتَواصُل مَع <c>/dynamic-attributes/templates/{scope}</c>.
/// التَطبيق يُسَجِّله في DI:
/// <code>services.AddScoped&lt;IAttributesStore, HttpAttributesStore&gt;();</code>
/// </summary>
public sealed class HttpAttributesStore : IAttributesStore
{
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<Guid, AttributeTemplate?> _cache = new();

    public HttpAttributesStore(HttpClient http) => _http = http;

    public async Task<AttributeTemplate?> GetTemplateAsync(Guid scopeId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(scopeId, out var cached)) return cached;
        try
        {
            // الـ envelope: { type, data: AttributeTemplate }. نَستَخرِج data.
            using var resp = await _http.GetAsync($"dynamic-attributes/templates/{scopeId}", ct);
            if (!resp.IsSuccessStatusCode) { _cache[scopeId] = null; return null; }
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, default, ct);
            if (!doc.RootElement.TryGetProperty("data", out var data))
            {
                _cache[scopeId] = null;
                return null;
            }
            var tpl = data.Deserialize<AttributeTemplate>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            _cache[scopeId] = tpl;
            return tpl;
        }
        catch
        {
            return null;
        }
    }

    public void InvalidateTemplate(Guid scopeId) => _cache.TryRemove(scopeId, out _);
}
