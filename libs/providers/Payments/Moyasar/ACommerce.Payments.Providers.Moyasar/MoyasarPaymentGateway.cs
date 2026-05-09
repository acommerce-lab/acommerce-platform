using ACommerce.Payments.Operations.Abstractions;
using ACommerce.Payments.Providers.Moyasar.Options;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ACommerce.Payments.Providers.Moyasar;

/// <summary>Moyasar payment gateway provider (main Saudi gateway).</summary>
public class MoyasarPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly MoyasarOptions _options;
    private readonly ILogger<MoyasarPaymentGateway> _logger;

    public string Name => "moyasar";

    public MoyasarPaymentGateway(HttpClient http, MoyasarOptions options, ILogger<MoyasarPaymentGateway> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InitiateResult> InitiateAsync(PaymentRequest request, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                amount = (int)(request.Amount * 100), // Moyasar uses halalas (smallest unit)
                currency = request.Currency,
                description = request.Description ?? $"Order {request.OrderReference}",
                callback_url = request.ReturnUrl ?? _options.CallbackUrl,
                source = new
                {
                    type = "creditcard"
                },
                metadata = new
                {
                    order_reference = request.OrderReference,
                    customer_id = request.CustomerId
                }
            };

            using var response = await PostAsync("/payments", payload, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Moyasar] Initiate failed: {Status} - {Body}", response.StatusCode, raw);
                return new InitiateResult(false, null, null, $"HTTP {(int)response.StatusCode}: {ExtractError(raw)}");
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var paymentId = root.TryGetProperty("id", out var id) ? id.GetString() : null;
            string? paymentUrl = null;

            if (root.TryGetProperty("source", out var source) &&
                source.TryGetProperty("transaction_url", out var txUrl))
            {
                paymentUrl = txUrl.GetString();
            }

            if (string.IsNullOrEmpty(paymentId))
                return new InitiateResult(false, null, null, "No payment id in Moyasar response");

            _logger.LogInformation("[Moyasar] Initiated payment {PaymentId}", paymentId);

            return new InitiateResult(
                Succeeded: true,
                PaymentReference: paymentId,
                PaymentUrl: paymentUrl,
                ProviderData: new Dictionary<string, string> { ["rawResponse"] = raw });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Moyasar] Initiate exception");
            return new InitiateResult(false, null, null, ex.Message);
        }
    }

    public Task<CaptureResult> CaptureAsync(string paymentReference, decimal? amount = null, CancellationToken ct = default)
    {
        throw new NotImplementedException("Moyasar does not support manual capture; payments are captured automatically.");
    }

    public Task<RefundResult> RefundAsync(string paymentReference, decimal? amount = null, string? reason = null, CancellationToken ct = default)
    {
        throw new NotImplementedException("Refund via Moyasar is not yet implemented.");
    }

    public Task<VoidResult> VoidAsync(string paymentReference, CancellationToken ct = default)
    {
        throw new NotImplementedException("Void via Moyasar is not yet implemented.");
    }

    public Task<PaymentStatusResult> GetStatusAsync(string paymentReference, CancellationToken ct = default)
    {
        throw new NotImplementedException("GetStatus via Moyasar is not yet implemented.");
    }

    public Task<WebhookResult> ParseWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            var paymentId = root.TryGetProperty("id", out var id) ? id.GetString() : null;
            var statusStr = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            var amountVal = root.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Number
                ? (decimal?)a.GetDecimal() / 100
                : null;

            var status = statusStr?.ToLowerInvariant() switch
            {
                "initiated" => PaymentStatus.Pending,
                "paid" => PaymentStatus.Completed,
                "failed" => PaymentStatus.Failed,
                "authorized" => PaymentStatus.Authorized,
                "captured" => PaymentStatus.Captured,
                "refunded" => PaymentStatus.Refunded,
                "voided" => PaymentStatus.Voided,
                _ => PaymentStatus.Custom(statusStr ?? "unknown")
            };

            return Task.FromResult(new WebhookResult(
                IsValid: true,
                PaymentReference: paymentId,
                Status: status,
                Amount: amountVal,
                EventType: statusStr));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Moyasar] Webhook parse exception");
            return Task.FromResult(new WebhookResult(false, Error: ex.Message));
        }
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}{path}")
        {
            Content = content
        };

        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.SecretKey}:"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        return await _http.SendAsync(request, ct);
    }

    private static string ExtractError(string rawResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawResponse);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString() ?? rawResponse;
        }
        catch { /* ignore */ }
        return rawResponse.Length > 200 ? rawResponse[..200] : rawResponse;
    }
}
