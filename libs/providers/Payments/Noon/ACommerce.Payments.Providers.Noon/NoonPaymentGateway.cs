using ACommerce.Payments.Operations.Abstractions;
using ACommerce.Payments.Providers.Noon.Options;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ACommerce.Payments.Providers.Noon;

/// <summary>
/// بوابة دفع Noon Payments (noonpay).
///
/// تستخدم Noon REST API:
///   POST /payment/v1/order           → Initiate
///   POST /payment/v1/order/capture   → Capture
///   POST /payment/v1/order/refund    → Refund
///   POST /payment/v1/order/void      → Void
///   GET  /payment/v1/order/{id}      → Status
/// </summary>
public class NoonPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly NoonOptions _options;
    private readonly ILogger<NoonPaymentGateway> _logger;

    public string Name => "noon";

    public NoonPaymentGateway(HttpClient http, NoonOptions options, ILogger<NoonPaymentGateway> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // =============================================
    // Initiate
    // =============================================

    public async Task<InitiateResult> InitiateAsync(PaymentRequest request, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                apiOperation = _options.Category, // "SALE" or "AUTHORIZE"
                order = new
                {
                    reference = request.OrderReference,
                    amount = request.Amount.ToString("F2"),
                    currency = request.Currency,
                    description = request.Description ?? $"Order {request.OrderReference}",
                    channel = _options.Channel,
                    returnUrl = request.ReturnUrl,
                    @public = new
                    {
                        orderReference = request.OrderReference
                    }
                },
                configuration = new
                {
                    returnUrl = request.ReturnUrl,
                    webhookUrl = request.WebhookUrl
                },
                customer = request.CustomerId != null
                    ? new { reference = request.CustomerId, email = request.CustomerEmail }
                    : null
            };

            using var response = await PostAsync("/payment/v1/order", payload, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Noon] Initiate failed: {Status} - {Body}", response.StatusCode, raw);
                return new InitiateResult(false, null, null,
                    $"HTTP {(int)response.StatusCode}: {ExtractError(raw)}");
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            // Noon يرجع result.order.id و checkoutData.postUrl
            string? orderId = null;
            string? paymentUrl = null;

            if (root.TryGetProperty("result", out var result))
            {
                if (result.TryGetProperty("order", out var order) &&
                    order.TryGetProperty("id", out var id))
                {
                    orderId = id.ValueKind == JsonValueKind.Number
                        ? id.GetInt64().ToString()
                        : id.GetString();
                }

                if (result.TryGetProperty("checkoutData", out var checkout) &&
                    checkout.TryGetProperty("postUrl", out var url))
                {
                    paymentUrl = url.GetString();
                }
            }

            if (string.IsNullOrEmpty(orderId))
                return new InitiateResult(false, null, null, "No order id in Noon response");

            _logger.LogInformation("[Noon] Initiated order {OrderId}", orderId);

            return new InitiateResult(
                Succeeded: true,
                PaymentReference: orderId,
                PaymentUrl: paymentUrl,
                ProviderData: new Dictionary<string, string> { ["rawResponse"] = raw });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Noon] Initiate exception");
            return new InitiateResult(false, null, null, ex.Message);
        }
    }

    // =============================================
    // Capture
    // =============================================

    public async Task<CaptureResult> CaptureAsync(string paymentReference, decimal? amount = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                apiOperation = "CAPTURE",
                order = new
                {
                    id = paymentReference
                },
                transaction = amount.HasValue
                    ? new { amount = amount.Value.ToString("F2") }
                    : null
            };

            using var response = await PostAsync("/payment/v1/order/capture", payload, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return new CaptureResult(false, null, null, $"HTTP {(int)response.StatusCode}: {ExtractError(raw)}");

            using var doc = JsonDocument.Parse(raw);
            var txId = ExtractTransactionId(doc.RootElement);

            _logger.LogInformation("[Noon] Captured order {Id}, tx: {TxId}", paymentReference, txId);
            return new CaptureResult(true, txId, amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Noon] Capture exception");
            return new CaptureResult(false, null, null, ex.Message);
        }
    }

    // =============================================
    // Refund
    // =============================================

    public async Task<RefundResult> RefundAsync(
        string paymentReference, decimal? amount = null, string? reason = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                apiOperation = "REFUND",
                order = new { id = paymentReference },
                transaction = amount.HasValue
                    ? new { amount = amount.Value.ToString("F2") }
                    : null
            };

            using var response = await PostAsync("/payment/v1/order/refund", payload, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return new RefundResult(false, null, null, $"HTTP {(int)response.StatusCode}: {ExtractError(raw)}");

            using var doc = JsonDocument.Parse(raw);
            var refundId = ExtractTransactionId(doc.RootElement);

            _logger.LogInformation("[Noon] Refunded order {Id}, refundId: {RefundId}", paymentReference, refundId);
            return new RefundResult(true, refundId, amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Noon] Refund exception");
            return new RefundResult(false, null, null, ex.Message);
        }
    }

    // =============================================
    // Void
    // =============================================

    public async Task<VoidResult> VoidAsync(string paymentReference, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                apiOperation = "REVERSE",
                order = new { id = paymentReference }
            };

            using var response = await PostAsync("/payment/v1/order/reverse", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                var raw = await response.Content.ReadAsStringAsync(ct);
                return new VoidResult(false, $"HTTP {(int)response.StatusCode}: {ExtractError(raw)}");
            }

            _logger.LogInformation("[Noon] Voided order {Id}", paymentReference);
            return new VoidResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Noon] Void exception");
            return new VoidResult(false, ex.Message);
        }
    }

    // =============================================
    // Get Status
    // =============================================

    public async Task<PaymentStatusResult> GetStatusAsync(string paymentReference, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_options.GetBaseUrl()}/payment/v1/order/{paymentReference}");
            AddAuthHeaders(request);

            using var response = await _http.SendAsync(request, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return new PaymentStatusResult(false, PaymentStatus.Failed, null,
                    $"HTTP {(int)response.StatusCode}: {ExtractError(raw)}");

            using var doc = JsonDocument.Parse(raw);
            var status = ExtractStatus(doc.RootElement);
            var amount = ExtractAmount(doc.RootElement);

            return new PaymentStatusResult(true, status, amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Noon] GetStatus exception");
            return new PaymentStatusResult(false, PaymentStatus.Failed, null, ex.Message);
        }
    }

    // =============================================
    // Webhook Parsing
    // =============================================

    public Task<WebhookResult> ParseWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken ct = default)
    {
        try
        {
            // التحقق من التوقيع إن وُجد
            if (!string.IsNullOrEmpty(_options.WebhookSecret))
            {
                if (!headers.TryGetValue("X-Noon-Signature", out var signature) ||
                    !VerifySignature(rawBody, signature, _options.WebhookSecret))
                {
                    return Task.FromResult(new WebhookResult(false, Error: "invalid_signature"));
                }
            }

            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            var eventType = root.TryGetProperty("eventType", out var et) ? et.GetString() : null;
            var orderId = root.TryGetProperty("orderId", out var oid)
                ? (oid.ValueKind == JsonValueKind.Number ? oid.GetInt64().ToString() : oid.GetString())
                : null;

            var status = ExtractStatus(root);
            var amount = ExtractAmount(root);

            return Task.FromResult(new WebhookResult(
                IsValid: true,
                PaymentReference: orderId,
                Status: status,
                Amount: amount,
                EventType: eventType));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Noon] Webhook parse exception");
            return Task.FromResult(new WebhookResult(false, Error: ex.Message));
        }
    }

    // =============================================
    // Helpers
    // =============================================

    private async Task<HttpResponseMessage> PostAsync(string path, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.GetBaseUrl()}{path}")
        {
            Content = content
        };
        AddAuthHeaders(request);

        return await _http.SendAsync(request, ct);
    }

    private void AddAuthHeaders(HttpRequestMessage request)
    {
        // Noon uses "Key_Test" or "Key_Live" + ApiKey, and optional Business/Application identifier
        var keyPrefix = _options.Mode == NoonMode.Live ? "Key_Live" : "Key_Test";
        request.Headers.TryAddWithoutValidation("Authorization", $"{keyPrefix} {_options.ApiKey}");

        if (!string.IsNullOrEmpty(_options.BusinessIdentifier))
            request.Headers.TryAddWithoutValidation("X-Business-Identifier", _options.BusinessIdentifier);

        if (!string.IsNullOrEmpty(_options.ApplicationIdentifier))
            request.Headers.TryAddWithoutValidation("X-Application-Identifier", _options.ApplicationIdentifier);

        request.Headers.TryAddWithoutValidation("Accept", "application/json");
    }

    private static PaymentStatus ExtractStatus(JsonElement root)
    {
        // Noon يرجع status في result.order.status غالباً
        string? rawStatus = null;

        if (root.TryGetProperty("result", out var result) &&
            result.TryGetProperty("order", out var order) &&
            order.TryGetProperty("status", out var s))
        {
            rawStatus = s.GetString();
        }
        else if (root.TryGetProperty("status", out var directStatus))
        {
            rawStatus = directStatus.GetString();
        }

        return (rawStatus?.ToUpperInvariant()) switch
        {
            "INITIATED" => PaymentStatus.Pending,
            "PENDING" => PaymentStatus.Pending,
            "AUTHORIZED" => PaymentStatus.Authorized,
            "CAPTURED" => PaymentStatus.Captured,
            "SUCCESS" => PaymentStatus.Completed,
            "COMPLETED" => PaymentStatus.Completed,
            "FAILED" => PaymentStatus.Failed,
            "CANCELLED" => PaymentStatus.Cancelled,
            "REVERSED" => PaymentStatus.Voided,
            "REFUNDED" => PaymentStatus.Refunded,
            "EXPIRED" => PaymentStatus.Expired,
            _ => PaymentStatus.Custom(rawStatus ?? "unknown")
        };
    }

    private static decimal? ExtractAmount(JsonElement root)
    {
        if (root.TryGetProperty("result", out var result) &&
            result.TryGetProperty("order", out var order) &&
            order.TryGetProperty("amount", out var a))
        {
            if (a.ValueKind == JsonValueKind.Number)
                return a.GetDecimal();
            if (a.ValueKind == JsonValueKind.String && decimal.TryParse(a.GetString(), out var val))
                return val;
        }
        return null;
    }

    private static string? ExtractTransactionId(JsonElement root)
    {
        if (root.TryGetProperty("result", out var result) &&
            result.TryGetProperty("transactions", out var txs) &&
            txs.ValueKind == JsonValueKind.Array &&
            txs.GetArrayLength() > 0)
        {
            var first = txs[0];
            if (first.TryGetProperty("id", out var id))
                return id.ValueKind == JsonValueKind.Number ? id.GetInt64().ToString() : id.GetString();
        }
        return null;
    }

    private static string ExtractError(string rawResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawResponse);
            if (doc.RootElement.TryGetProperty("resultCode", out var code) &&
                doc.RootElement.TryGetProperty("message", out var msg))
            {
                return $"{code.GetRawText()}: {msg.GetString()}";
            }
        }
        catch { /* ignore */ }
        return rawResponse.Length > 200 ? rawResponse[..200] : rawResponse;
    }

    private static bool VerifySignature(string body, string signature, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        using var hmac = new HMACSHA256(keyBytes);
        var computed = Convert.ToHexString(hmac.ComputeHash(bodyBytes)).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(signature.ToLowerInvariant()));
    }
}
