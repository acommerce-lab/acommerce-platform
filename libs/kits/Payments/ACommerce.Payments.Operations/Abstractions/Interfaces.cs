namespace ACommerce.Payments.Operations.Abstractions;

/// <summary>
/// بوابة الدفع - الواجهة الموحّدة التي يطبقها كل مزود.
/// Noon, Moyasar, Stripe, PayPal - كلها تطبق نفس العقد.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>اسم المزود: "noon", "moyasar", "stripe"</summary>
    string Name { get; }

    /// <summary>
    /// بدء عملية دفع - يُرجع رابط/معرف للمستخدم لإكمال الدفع.
    /// </summary>
    Task<InitiateResult> InitiateAsync(
        PaymentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// تأكيد/التقاط عملية مُفوّضة سابقاً (للـ 2-step capture).
    /// بعض المزودين يفصلون التفويض عن الالتقاط.
    /// </summary>
    Task<CaptureResult> CaptureAsync(
        string paymentReference,
        decimal? amount = null,
        CancellationToken ct = default);

    /// <summary>
    /// استرداد مبلغ - كلي أو جزئي.
    /// </summary>
    Task<RefundResult> RefundAsync(
        string paymentReference,
        decimal? amount = null,
        string? reason = null,
        CancellationToken ct = default);

    /// <summary>
    /// إلغاء عملية لم تُكمل بعد.
    /// </summary>
    Task<VoidResult> VoidAsync(
        string paymentReference,
        CancellationToken ct = default);

    /// <summary>
    /// الاستعلام عن حالة عملية.
    /// </summary>
    Task<PaymentStatusResult> GetStatusAsync(
        string paymentReference,
        CancellationToken ct = default);

    /// <summary>
    /// التحقق من توقيع webhook وإرجاع محتواه المُفسَّر.
    /// </summary>
    Task<WebhookResult> ParseWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken ct = default);
}

// =============================================
// طلبات ونتائج
// =============================================

/// <summary>طلب بدء دفع</summary>
public record PaymentRequest(
    decimal Amount,
    string Currency,
    string OrderReference,
    string? CustomerId = null,
    string? CustomerEmail = null,
    string? Description = null,
    string? ReturnUrl = null,
    string? WebhookUrl = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public record InitiateResult(
    bool Succeeded,
    string? PaymentReference,
    string? PaymentUrl,
    string? Error = null,
    Dictionary<string, string>? ProviderData = null);

public record CaptureResult(
    bool Succeeded,
    string? TransactionId,
    decimal? CapturedAmount = null,
    string? Error = null);

public record RefundResult(
    bool Succeeded,
    string? RefundId,
    decimal? RefundedAmount = null,
    string? Error = null);

public record VoidResult(
    bool Succeeded,
    string? Error = null);

public record PaymentStatusResult(
    bool Succeeded,
    PaymentStatus Status,
    decimal? Amount = null,
    string? Error = null);

public record WebhookResult(
    bool IsValid,
    string? PaymentReference = null,
    PaymentStatus? Status = null,
    decimal? Amount = null,
    string? EventType = null,
    string? Error = null);

/// <summary>
/// استثناء الدفع
/// </summary>
public class PaymentException : Exception
{
    public string? ProviderCode { get; }
    public PaymentException(string message, string? providerCode = null) : base(message)
    {
        ProviderCode = providerCode;
    }
}
