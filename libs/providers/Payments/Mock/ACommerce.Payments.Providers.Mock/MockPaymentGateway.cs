using ACommerce.Payments.Operations.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ACommerce.Payments.Providers.Mock;

/// <summary>
/// مُزَوِّد دَفع وَهمي يُنَفِّذ <see cref="IPaymentGateway"/>:
/// <list type="bullet">
///   <item><b>InitiateAsync</b>: يُخَزِّن سِجلّ pending داخِليّاً، يُرجِع
///         <c>PaymentReference</c> + <c>PaymentUrl</c> لِصَفحَة وَهمِيَّة.
///         يَفشَل لَو المَبلَغ = <see cref="MockPaymentOptions.FailOnAmount"/>.</item>
///   <item><b>GetStatusAsync</b>: <c>Captured</c> بَعد مُرور
///         <see cref="MockPaymentOptions.AutoCaptureSeconds"/> مِن البِدء،
///         وإلّا <c>Authorized</c>.</item>
///   <item><b>CaptureAsync</b>: idempotent ⇒ يَنجَح دائِماً لِأَيّ ref مَوجود.</item>
///   <item><b>RefundAsync</b> / <b>VoidAsync</b>: يَنجَحان لِأَيّ ref مَوجود.</item>
///   <item><b>ParseWebhookAsync</b>: يَتَوَقَّع <c>{"ref":"…","status":"…"}</c>
///         في الـ raw body — يُستَخدَم لِاختِبار مَسار الـ webhook.</item>
/// </list>
///
/// تَطبيقات الإنتاج تَستَبدِله بِـ <c>NoonPaymentGateway</c> أَو
/// <c>MoyasarPaymentGateway</c>.
/// </summary>
public sealed class MockPaymentGateway : IPaymentGateway
{
    private readonly MockPaymentOptions _options;
    private readonly ConcurrentDictionary<string, MockEntry> _store = new();

    public MockPaymentGateway(IOptions<MockPaymentOptions> options)
    {
        _options = options.Value ?? new MockPaymentOptions();
    }

    public MockPaymentGateway() : this(Options.Create(new MockPaymentOptions())) { }

    public string Name => "mock";

    private sealed record MockEntry(
        string Reference,
        decimal Amount,
        string Currency,
        string OrderReference,
        DateTimeOffset InitiatedAt,
        PaymentStatus Status);

    public Task<InitiateResult> InitiateAsync(PaymentRequest request, CancellationToken ct = default)
    {
        if (_options.FailOnAmount is { } fail && request.Amount == fail)
            return Task.FromResult(new InitiateResult(
                Succeeded: false,
                PaymentReference: null,
                PaymentUrl: null,
                Error: $"mock_fail_on_amount:{fail}"));

        var reference = "mock_" + Guid.NewGuid().ToString("N")[..16];
        _store[reference] = new MockEntry(
            Reference: reference,
            Amount: request.Amount,
            Currency: request.Currency,
            OrderReference: request.OrderReference,
            InitiatedAt: DateTimeOffset.UtcNow,
            Status: PaymentStatus.Authorized);

        var url = _options.PaymentUrlTemplate.Replace("{ref}", reference);
        return Task.FromResult(new InitiateResult(
            Succeeded:        true,
            PaymentReference: reference,
            PaymentUrl:       url,
            ProviderData: new Dictionary<string, string>
            {
                ["autoCaptureSeconds"] = _options.AutoCaptureSeconds.ToString(),
                ["amount"]             = request.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["currency"]           = request.Currency,
            }));
    }

    public Task<CaptureResult> CaptureAsync(string paymentReference, decimal? amount = null,
                                            CancellationToken ct = default)
    {
        if (!_store.TryGetValue(paymentReference, out var e))
            return Task.FromResult(new CaptureResult(false, null, Error: "ref_not_found"));
        var captured = amount ?? e.Amount;
        _store[paymentReference] = e with { Status = PaymentStatus.Captured };
        return Task.FromResult(new CaptureResult(
            Succeeded: true,
            TransactionId: paymentReference,
            CapturedAmount: captured));
    }

    public Task<RefundResult> RefundAsync(string paymentReference, decimal? amount = null,
                                          string? reason = null, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(paymentReference, out var e))
            return Task.FromResult(new RefundResult(false, null, Error: "ref_not_found"));
        _store[paymentReference] = e with { Status = PaymentStatus.Refunded };
        return Task.FromResult(new RefundResult(
            Succeeded: true,
            RefundId: "rf_" + Guid.NewGuid().ToString("N")[..12],
            RefundedAmount: amount ?? e.Amount));
    }

    public Task<VoidResult> VoidAsync(string paymentReference, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(paymentReference, out var e))
            return Task.FromResult(new VoidResult(false, Error: "ref_not_found"));
        _store[paymentReference] = e with { Status = PaymentStatus.Cancelled };
        return Task.FromResult(new VoidResult(true));
    }

    public Task<PaymentStatusResult> GetStatusAsync(string paymentReference, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(paymentReference, out var e))
            return Task.FromResult(new PaymentStatusResult(false, PaymentStatus.Failed, Error: "ref_not_found"));

        // Auto-capture بَعد الزَمَن. يَبقى مَدى الحَياة ⇒ polling آمِن.
        var elapsed = (DateTimeOffset.UtcNow - e.InitiatedAt).TotalSeconds;
        var effective = e.Status == PaymentStatus.Authorized && elapsed >= _options.AutoCaptureSeconds
            ? PaymentStatus.Captured
            : e.Status;
        if (effective != e.Status)
            _store[paymentReference] = e with { Status = effective };

        return Task.FromResult(new PaymentStatusResult(
            Succeeded: true,
            Status: effective,
            Amount: e.Amount));
    }

    public Task<WebhookResult> ParseWebhookAsync(string rawBody,
                                                 IReadOnlyDictionary<string, string> headers,
                                                 CancellationToken ct = default)
    {
        // Mock يَتَوَقَّع JSON: {"ref":"…","status":"captured"|"failed"|"refunded"}
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var reference = root.GetProperty("ref").GetString() ?? "";
            var statusStr = root.TryGetProperty("status", out var s) ? (s.GetString() ?? "captured") : "captured";
            var status = statusStr.ToLowerInvariant() switch
            {
                "captured" or "paid" => PaymentStatus.Captured,
                "failed"             => PaymentStatus.Failed,
                "refunded"           => PaymentStatus.Refunded,
                "cancelled"          => PaymentStatus.Cancelled,
                _                    => PaymentStatus.Authorized,
            };
            decimal? amount = null;
            if (_store.TryGetValue(reference, out var e))
            {
                _store[reference] = e with { Status = status };
                amount = e.Amount;
            }
            return Task.FromResult(new WebhookResult(
                IsValid: true,
                PaymentReference: reference,
                Status: status,
                Amount: amount,
                EventType: "payment." + statusStr));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new WebhookResult(IsValid: false, Error: ex.Message));
        }
    }
}
