using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using ACommerce.Kits.Taxonomy.Operations;

namespace ACommerce.Kits.Taxonomy.Frontend.Customer.Stores;

/// <summary>
/// تَنفيذ افتِراضي لِـ <see cref="ITaxonomyStore"/> يَستَخدِم
/// <c>HttpClient</c> المَحقون. يَجلِب الشَجَرَة كامِلَة مَرَّة واحِدَة
/// لِكُلّ rootCode + يَحفَظها في cache. <c>FindByCode</c>/<c>GetPath</c>/
/// <c>GetChildren</c> in-memory بَعد ذلك.
/// </summary>
public sealed class HttpTaxonomyStore : ITaxonomyStore
{
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, IReadOnlyList<ITaxonomyNode>> _cache = new();
    private readonly ConcurrentDictionary<string, Dictionary<string, ITaxonomyNode>> _byCode = new();

    public HttpTaxonomyStore(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<ITaxonomyNode>> GetTreeAsync(string rootCode, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(rootCode, out var cached)) return cached;
        try
        {
            using var resp = await _http.GetAsync($"taxonomy/{Uri.EscapeDataString(rootCode)}", ct);
            if (!resp.IsSuccessStatusCode) { _cache[rootCode] = Array.Empty<ITaxonomyNode>(); return _cache[rootCode]; }
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, default, ct);
            if (!doc.RootElement.TryGetProperty("data", out var data))
            {
                _cache[rootCode] = Array.Empty<ITaxonomyNode>();
                return _cache[rootCode];
            }
            var nodes = data.Deserialize<List<TaxonomyNodeView>>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? new();
            var list = (IReadOnlyList<ITaxonomyNode>)nodes.Cast<ITaxonomyNode>()
                .OrderBy(n => n.SortOrder).ThenBy(n => n.Code).ToList();
            _cache[rootCode] = list;
            _byCode[rootCode] = list.ToDictionary(n => n.Code, StringComparer.OrdinalIgnoreCase);
            return list;
        }
        catch
        {
            _cache[rootCode] = Array.Empty<ITaxonomyNode>();
            return _cache[rootCode];
        }
    }

    public ITaxonomyNode? FindByCode(string rootCode, string code)
    {
        if (!_byCode.TryGetValue(rootCode, out var map)) return null;
        return map.TryGetValue(code, out var n) ? n : null;
    }

    public IReadOnlyList<ITaxonomyNode> GetPath(string rootCode, string code)
    {
        var node = FindByCode(rootCode, code);
        if (node is null) return Array.Empty<ITaxonomyNode>();

        if (!_byCode.TryGetValue(rootCode, out var map)) return Array.Empty<ITaxonomyNode>();
        var byId = map.Values.ToDictionary(n => n.Id);

        var path = new List<ITaxonomyNode> { node };
        var cur = node;
        while (cur.ParentId is { } pid && byId.TryGetValue(pid, out var parent))
        {
            path.Insert(0, parent);
            cur = parent;
        }
        return path;
    }

    public IReadOnlyList<ITaxonomyNode> GetChildren(string rootCode, Guid? parentId)
    {
        if (!_cache.TryGetValue(rootCode, out var list)) return Array.Empty<ITaxonomyNode>();
        return list.Where(n => n.ParentId == parentId && n.IsActive)
                   .OrderBy(n => n.SortOrder).ThenBy(n => n.Code)
                   .ToList();
    }

    public void Invalidate(string rootCode)
    {
        _cache.TryRemove(rootCode, out _);
        _byCode.TryRemove(rootCode, out _);
    }
}
