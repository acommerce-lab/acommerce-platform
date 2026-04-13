using System.Net.Http.Json;
using ACommerce.Client.Operations;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using Microsoft.Extensions.Logging;

namespace ACommerce.Client.Http;

/// <summary>
/// مُرسل HTTP محاسبي.
/// كل استدعاء HTTP يُمثَّل بقيد محاسبي خاص به (http.send) - يحوي:
///   - From: Client (المستهلك)
///   - To: Server (المُصدر)
///   - Tags: http.method, http.url, embedded_op_type, status_code...
///
/// القيد الـ embedded (مثل listing.create) يُرسل كحمولة JSON
/// ويعود مغلَّفاً في OperationEnvelope.
/// </summary>
public class HttpDispatcher : IOperationDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly HttpRouteRegistry _routes;
    private readonly OpEngine _engine;
    private readonly ILogger<HttpDispatcher> _logger;
    private readonly string _clientPartyId;
    private readonly string _serverPartyId;

    public HttpDispatcher(
        HttpClient httpClient,
        HttpRouteRegistry routes,
        OpEngine engine,
        ILogger<HttpDispatcher> logger,
        HttpDispatcherOptions? options = null)
    {
        _httpClient = httpClient;
        _routes = routes;
        _engine = engine;
        _logger = logger;
        _clientPartyId = options?.ClientPartyId ?? $"Client:{Environment.MachineName}";
        _serverPartyId = options?.ServerPartyId ?? $"Server:{httpClient.BaseAddress?.Host ?? "unknown"}";
    }

    public async Task<OperationEnvelope<T>> DispatchAsync<T>(
        Operation embeddedOp,
        object? payload = null,
        CancellationToken ct = default)
    {
        var route = _routes.Resolve(embeddedOp.Type);
        if (route == null)
            throw new InvalidOperationException(
                $"No HTTP route registered for operation type '{embeddedOp.Type}'");

        OperationEnvelope<T>? envelope = null;

        // استبدال معاملات القالب من tags العملية — {booking_id} → قيمة tag
        var resolvedUrl = route.UrlTemplate;
        foreach (var tag in embeddedOp.Tags)
            resolvedUrl = resolvedUrl.Replace("{" + tag.Key + "}", Uri.EscapeDataString(tag.Value));

        // قيد نقل http.send - محاسبي بذاته
        var transportOp = Entry.Create("http.send")
            .Describe($"{route.Method} {resolvedUrl} (carries {embeddedOp.Type})")
            .From(_clientPartyId, 1, ("role", "client"))
            .To(_serverPartyId, 1, ("role", "server"))
            .Tag("http.method", route.Method.ToString())
            .Tag("http.url", resolvedUrl)
            .Tag("embedded_op_type", embeddedOp.Type)
            .Tag("embedded_op_id", embeddedOp.Id.ToString())
            .Execute(async ctx =>
            {
                using var request = new HttpRequestMessage(route.Method, resolvedUrl);
                if (payload != null && (route.Method == HttpMethod.Post || route.Method == HttpMethod.Put || route.Method == HttpMethod.Patch))
                {
                    request.Content = JsonContent.Create(payload);
                }

                using var response = await _httpClient.SendAsync(request, ctx.CancellationToken);
                ctx.Set("status_code", (int)response.StatusCode);

                var serverEnv = await response.Content
                    .ReadFromJsonAsync<OperationEnvelope<T>>(ctx.CancellationToken);

                envelope = serverEnv ?? new OperationEnvelope<T>();
                ctx.Set("envelope", envelope);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[HttpDispatcher] {Status} {Method} {Url}",
                        (int)response.StatusCode, route.Method, resolvedUrl);
                }
            })
            .Build();

        await _engine.ExecuteAsync(transportOp, ct);

        return envelope ?? new OperationEnvelope<T>
        {
            Error = new OperationError { Code = "no_response" }
        };
    }
}

public class HttpDispatcherOptions
{
    public string? ClientPartyId { get; set; }
    public string? ServerPartyId { get; set; }
}
