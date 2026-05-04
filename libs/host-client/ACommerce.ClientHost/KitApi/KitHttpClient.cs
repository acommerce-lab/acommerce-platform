using System.Net.Http.Json;
using System.Text.Json;
using ACommerce.OperationEngine.Wire;

namespace ACommerce.ClientHost.KitApi;

/// <summary>
/// عميل HTTP موحَّد لكلّ kit api clients. يَلفّ <c>HttpClient</c> +
/// pipeline من analyzers + interceptors. كلّ <c>HttpXxxApiClient</c>
/// في كلّ الكيتس يَستهلك هذه الفئة، فلا يُكَرِّر منطق الـ envelope
/// peeling أو الـ telemetry.
///
/// <para>التَدَفّق:
/// <list type="number">
///   <item>analyzers تُفحَص (pre-flight). فشل ⇒ لا إرسال + رسالة خطأ.</item>
///   <item>interceptors.BeforeAsync (telemetry، tracing، …).</item>
///   <item>HTTP send + قراءة raw body.</item>
///   <item>interceptors.AfterAsync (logging، metrics).</item>
///   <item>تَقشير <c>OperationEnvelope&lt;T&gt;</c> لو الردّ على شَكله،
///         وإلّا deserialize مباشرة لـ T.</item>
/// </list></para>
///
/// <para>أيّ خَطأ شبكيّ أو parsing يُعاد كـ <see cref="KitApiResult{T}"/>
/// مع <c>Success=false</c> + رسالة. الـ kit api client يُقرّر ماذا يَفعل
/// (يَترك state سابقاً، يَعرض toast، …).</para>
/// </summary>
public sealed class KitHttpClient
{
    private readonly HttpClient _http;
    private readonly IReadOnlyList<IKitApiAnalyzer> _analyzers;
    private readonly IReadOnlyList<IKitApiInterceptor> _interceptors;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public KitHttpClient(
        HttpClient http,
        IEnumerable<IKitApiAnalyzer>? analyzers = null,
        IEnumerable<IKitApiInterceptor>? interceptors = null)
    {
        _http = http;
        _analyzers    = analyzers?.ToArray()    ?? Array.Empty<IKitApiAnalyzer>();
        _interceptors = interceptors?.ToArray() ?? Array.Empty<IKitApiInterceptor>();
    }

    public Task<KitApiResult<T>> GetAsync<T>(string kit, string path, CancellationToken ct = default)
        => SendAsync<T>(kit, HttpMethod.Get, path, body: null, ct);

    public Task<KitApiResult<T>> PostAsync<T>(string kit, string path, object? body = null, CancellationToken ct = default)
        => SendAsync<T>(kit, HttpMethod.Post, path, body, ct);

    public Task<KitApiResult<T>> PutAsync<T>(string kit, string path, object? body = null, CancellationToken ct = default)
        => SendAsync<T>(kit, HttpMethod.Put, path, body, ct);

    public Task<KitApiResult<T>> DeleteAsync<T>(string kit, string path, CancellationToken ct = default)
        => SendAsync<T>(kit, HttpMethod.Delete, path, body: null, ct);

    public async Task<KitApiResult<T>> SendAsync<T>(
        string kit, HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        var req = new KitApiRequest { KitName = kit, Method = method.Method, Path = path, Body = body };

        // 1. analyzers
        foreach (var a in _analyzers)
        {
            var err = await a.CheckAsync(req, ct);
            if (!string.IsNullOrEmpty(err))
                return KitApiResult<T>.Fail($"{a.Name}: {err}");
        }

        // 2. before
        foreach (var i in _interceptors)
        {
            try { await i.BeforeAsync(req, ct); } catch { /* lossy */ }
        }

        // 3. send
        KitApiResponse resp;
        try
        {
            using var msg = new HttpRequestMessage(method, path);
            if (body is not null) msg.Content = JsonContent.Create(body, options: _json);
            using var http = await _http.SendAsync(msg, ct);
            var raw = await http.Content.ReadAsStringAsync(ct);
            resp = new KitApiResponse
            {
                StatusCode = (int)http.StatusCode,
                RawBody    = raw,
                IsSuccess  = http.IsSuccessStatusCode,
            };
        }
        catch (Exception ex)
        {
            resp = new KitApiResponse { StatusCode = 0, RawBody = "", IsSuccess = false, Exception = ex };
        }

        // 4. after
        foreach (var i in _interceptors)
        {
            try { await i.AfterAsync(req, resp, ct); } catch { /* lossy */ }
        }

        // 5. peel
        if (!resp.IsSuccess)
            return KitApiResult<T>.Fail(resp.Exception?.Message ?? $"http_{resp.StatusCode}");

        try { return KitApiResult<T>.Ok(PeelEnvelope<T>(resp.RawBody)); }
        catch (Exception ex) { return KitApiResult<T>.Fail($"parse_error: {ex.Message}"); }
    }

    /// <summary>
    /// يَستخرج T من ردّ JSON. الترتيب:
    ///   ١. <c>OperationEnvelope&lt;T&gt;</c> ⇒ <c>env.Data</c> لو Status=Success.
    ///   ٢. وإلّا deserialize مباشر لـ T (بَعض APIs لا تَستعمل envelope).
    /// </summary>
    private T? PeelEnvelope<T>(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return default;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("operation", out var opEl) &&
                root.TryGetProperty("data", out var dataEl))
            {
                var status = opEl.TryGetProperty("status", out var s) ? s.GetString() : null;
                if (status == "Success" && dataEl.ValueKind != JsonValueKind.Null)
                    return dataEl.Deserialize<T>(_json);
                return default;
            }
        }
        catch { /* لا envelope — جَرِّب direct */ }

        return JsonSerializer.Deserialize<T>(raw, _json);
    }
}

/// <summary>نَتيجة kit api call — Success/Fail بدون استثناءات.</summary>
public readonly struct KitApiResult<T>
{
    public bool Success  { get; }
    public T?   Data     { get; }
    public string? Error { get; }

    private KitApiResult(bool s, T? d, string? e) { Success = s; Data = d; Error = e; }

    public static KitApiResult<T> Ok(T? data)         => new(true,  data, null);
    public static KitApiResult<T> Fail(string error)  => new(false, default, error);
}
