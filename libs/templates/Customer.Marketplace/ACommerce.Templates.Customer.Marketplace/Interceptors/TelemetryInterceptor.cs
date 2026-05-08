using ACommerce.ClientHost.KitApi;
using Microsoft.Extensions.Logging;

namespace Ejar.Customer.UI.Interceptors;

/// <summary>
/// يُسَجِّل كلّ kit api call (kit، method، path، status، duration). يَجري
/// حول كلّ kit clients تلقائياً بمجرّد تسجيله في الـ pipeline. مُلائم
/// للتطوير + أساس لـ metrics/Sentry لاحقاً.
/// </summary>
public sealed class TelemetryInterceptor : IKitApiInterceptor
{
    private readonly ILogger<TelemetryInterceptor> _logger;
    private readonly Dictionary<string, long> _starts = new();

    public TelemetryInterceptor(ILogger<TelemetryInterceptor> logger) => _logger = logger;

    public string Name => "Telemetry";

    public Task BeforeAsync(KitApiRequest request, CancellationToken ct)
    {
        _starts[Key(request)] = Environment.TickCount64;
        return Task.CompletedTask;
    }

    public Task AfterAsync(KitApiRequest request, KitApiResponse response, CancellationToken ct)
    {
        var key = Key(request);
        var dur = _starts.Remove(key, out var t0) ? Environment.TickCount64 - t0 : -1;
        _logger.LogInformation(
            "kit-api {Kit} {Method} {Path} → {Status} ({Duration}ms)",
            request.KitName, request.Method, request.Path, response.StatusCode, dur);
        return Task.CompletedTask;
    }

    private static string Key(KitApiRequest r) => $"{r.KitName}|{r.Method}|{r.Path}";
}
