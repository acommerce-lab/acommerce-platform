using ACommerce.OperationEngine.Core;
using ACommerce.Payments.Operations.Abstractions;
using ACommerce.Payments.Operations.Operations;

namespace ACommerce.Payments.Operations;

/// <summary>
/// تهيئة الدفع.
///
/// services.AddPayments(config => {
///     config.AddGateway(new NoonPaymentGateway(...));
///     config.AddGateway(new MoyasarPaymentGateway(...));
/// });
/// </summary>
public class PaymentConfig
{
    internal Dictionary<string, IPaymentGateway> Gateways { get; } = new();

    public PaymentConfig AddGateway(IPaymentGateway gateway)
    {
        Gateways[gateway.Name] = gateway;
        return this;
    }
}

/// <summary>
/// واجهة المطور البسيطة للدفع.
///
///   var r = await payments.InitiateAsync("noon", customerId, request);
///   if (r.Succeeded) redirect(r.PaymentUrl);
/// </summary>
public class PaymentService
{
    private readonly PaymentConfig _config;
    private readonly OpEngine _engine;

    public PaymentService(PaymentConfig config, OpEngine engine)
    {
        _config = config;
        _engine = engine;
    }

    public async Task<PaymentInitiateOutcome> InitiateAsync(
        string gatewayName,
        string customerId,
        PaymentRequest request,
        CancellationToken ct = default)
    {
        var gateway = GetGateway(gatewayName);
        var op = PaymentOps.Initiate(PayPartyId.Customer(customerId), request, gateway);
        var result = await _engine.ExecuteAsync(op, ct);

        if (!result.Success)
        {
            result.Context!.TryGet<string>("error", out var err);
            return new PaymentInitiateOutcome(false, null, null, err);
        }

        result.Context!.TryGet<string>("paymentReference", out var reference);
        result.Context!.TryGet<string>("paymentUrl", out var url);
        return new PaymentInitiateOutcome(true, reference, url, null);
    }

    public async Task<PaymentCaptureOutcome> CaptureAsync(
        string gatewayName,
        string merchantId,
        string paymentReference,
        decimal amount,
        string currency,
        CancellationToken ct = default)
    {
        var gateway = GetGateway(gatewayName);
        var op = PaymentOps.Capture(PayPartyId.Merchant(merchantId), paymentReference, amount, currency, gateway);
        var result = await _engine.ExecuteAsync(op, ct);

        if (!result.Success)
        {
            result.Context!.TryGet<string>("error", out var err);
            return new PaymentCaptureOutcome(false, null, null, err);
        }

        result.Context!.TryGet<string>("transactionId", out var tx);
        result.Context!.TryGet<decimal?>("capturedAmount", out var cap);
        return new PaymentCaptureOutcome(true, tx, cap, null);
    }

    public async Task<PaymentRefundOutcome> RefundAsync(
        string gatewayName,
        string merchantId,
        string customerId,
        string paymentReference,
        decimal amount,
        string currency,
        string? reason = null,
        CancellationToken ct = default)
    {
        var gateway = GetGateway(gatewayName);
        var op = PaymentOps.Refund(
            PayPartyId.Merchant(merchantId),
            PayPartyId.Customer(customerId),
            paymentReference, amount, currency, gateway, reason);

        var result = await _engine.ExecuteAsync(op, ct);

        if (!result.Success)
        {
            result.Context!.TryGet<string>("error", out var err);
            return new PaymentRefundOutcome(false, null, null, err);
        }

        result.Context!.TryGet<string>("refundId", out var rid);
        result.Context!.TryGet<decimal?>("refundedAmount", out var ra);
        return new PaymentRefundOutcome(true, rid, ra, null);
    }

    public async Task<bool> VoidAsync(
        string gatewayName,
        string paymentReference,
        CancellationToken ct = default)
    {
        var gateway = GetGateway(gatewayName);
        var op = PaymentOps.Void(paymentReference, gateway);
        var result = await _engine.ExecuteAsync(op, ct);
        return result.Success;
    }

    public async Task<PaymentStatus?> QueryAsync(
        string gatewayName,
        string paymentReference,
        CancellationToken ct = default)
    {
        var gateway = GetGateway(gatewayName);
        var op = PaymentOps.Query(paymentReference, gateway);
        var result = await _engine.ExecuteAsync(op, ct);

        if (!result.Success) return null;
        result.Context!.TryGet<PaymentStatus>("status", out var status);
        return status;
    }

    public Task<WebhookResult> ParseWebhookAsync(
        string gatewayName,
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken ct = default)
    {
        var gateway = GetGateway(gatewayName);
        return gateway.ParseWebhookAsync(rawBody, headers, ct);
    }

    private IPaymentGateway GetGateway(string name)
    {
        if (!_config.Gateways.TryGetValue(name, out var gw))
            throw new ArgumentException($"Payment gateway '{name}' not registered.");
        return gw;
    }
}

// نتائج معالجة للعمليات
public record PaymentInitiateOutcome(bool Succeeded, string? PaymentReference, string? PaymentUrl, string? Error);
public record PaymentCaptureOutcome(bool Succeeded, string? TransactionId, decimal? Amount, string? Error);
public record PaymentRefundOutcome(bool Succeeded, string? RefundId, decimal? Amount, string? Error);
