using System.Net.Http.Json;

namespace Order.Api.Services;

/// <summary>
/// Sends a webhook to Vendor.Api when a new order is created.
/// This is the "payment request" leg — Order.Api tells the vendor's
/// service that a customer has placed an order and is waiting for
/// acceptance. Vendor.Api responds via the /vendor-callback endpoint.
/// </summary>
public class VendorApiNotifier
{
    private readonly HttpClient _http;
    private readonly ILogger<VendorApiNotifier> _logger;

    public VendorApiNotifier(HttpClient http, ILogger<VendorApiNotifier> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task NotifyNewOrderAsync(
        Guid orderApiId,
        Guid vendorId,
        string orderNumber,
        string customerName,
        string? customerPhone,
        decimal total,
        string currency,
        string? itemsSummary,
        int pickupType,
        string? carModel,
        string? carColor,
        string? carPlate,
        string? customerNotes,
        CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/vendor-orders/incoming", new
            {
                orderApiId,
                vendorId,
                orderNumber,
                customerName,
                customerPhone,
                total,
                currency,
                itemsSummary,
                pickupType,
                carModel,
                carColor,
                carPlate,
                customerNotes,
            }, ct);

            _logger.LogInformation(
                "Notified Vendor.Api about order {OrderNumber} → {Status}",
                orderNumber, resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify Vendor.Api about order {OrderNumber}", orderNumber);
        }
    }
}
