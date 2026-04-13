using System.Net.Http.Json;

namespace Vendor.Api.Services;

/// <summary>
/// HTTP client that sends vendor decisions back to Order.Api.
/// This is the "payment gateway callback" — Vendor.Api notifies
/// Order.Api that the vendor accepted/rejected/delivered an order.
/// </summary>
public class OrderApiCallback
{
    private readonly HttpClient _http;
    private readonly ILogger<OrderApiCallback> _logger;

    public OrderApiCallback(HttpClient http, ILogger<OrderApiCallback> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task NotifyStatusAsync(Guid orderApiId, string action, CancellationToken ct = default)
    {
        var path = $"/api/orders/{orderApiId}/vendor-callback";
        try
        {
            var resp = await _http.PostAsJsonAsync(path, new { action }, ct);
            _logger.LogInformation("Callback to Order.Api: {Path} action={Action} → {Status}",
                path, action, resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to callback Order.Api: {Path} action={Action}", path, action);
        }
    }
}
