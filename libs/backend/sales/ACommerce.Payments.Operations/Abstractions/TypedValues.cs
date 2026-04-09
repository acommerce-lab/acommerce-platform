using ACommerce.OperationEngine.Core;
namespace ACommerce.Payments.Operations.Abstractions;

/// <summary>
/// حالة الدفع.
/// </summary>
public sealed class PaymentStatus
{
    public string Value { get; }
    private PaymentStatus(string value) => Value = value;

    public static readonly PaymentStatus Pending = new("pending");
    public static readonly PaymentStatus Authorized = new("authorized");
    public static readonly PaymentStatus Captured = new("captured");
    public static readonly PaymentStatus Completed = new("completed");
    public static readonly PaymentStatus Failed = new("failed");
    public static readonly PaymentStatus Cancelled = new("cancelled");
    public static readonly PaymentStatus Refunded = new("refunded");
    public static readonly PaymentStatus PartiallyRefunded = new("partially_refunded");
    public static readonly PaymentStatus Voided = new("voided");
    public static readonly PaymentStatus Expired = new("expired");

    public static PaymentStatus Custom(string v) => new(v);
    public override string ToString() => Value;
    public override bool Equals(object? obj) => obj is PaymentStatus ps && ps.Value == Value;
    public override int GetHashCode() => Value.GetHashCode();
    public static implicit operator string(PaymentStatus ps) => ps.Value;
}

/// <summary>
/// العملة - قيم شائعة + مخصص.
/// </summary>
public sealed class Currency
{
    public string Value { get; }
    private Currency(string value) => Value = value;

    public static readonly Currency SAR = new("SAR");
    public static readonly Currency AED = new("AED");
    public static readonly Currency USD = new("USD");
    public static readonly Currency EUR = new("EUR");
    public static readonly Currency GBP = new("GBP");
    public static readonly Currency KWD = new("KWD");
    public static readonly Currency BHD = new("BHD");
    public static readonly Currency OMR = new("OMR");
    public static readonly Currency QAR = new("QAR");
    public static readonly Currency EGP = new("EGP");

    public static Currency Of(string code) => new(code);
    public override string ToString() => Value;
    public static implicit operator string(Currency c) => c.Value;
}

/// <summary>
/// مفاتيح علامات الدفع.
/// </summary>
public static class PayTags
{
    /// <summary>اسم البوابة. القيم: "noon", "moyasar", "stripe"</summary>
    public static readonly TagKey Gateway = new("gateway");

    /// <summary>العملة. القيم: "SAR", "USD", ...</summary>
    public static readonly TagKey Currency = new("currency");

    /// <summary>حالة الدفع. القيم: "pending", "captured", ...</summary>
    public static readonly TagKey Status = new("payment_status");

    /// <summary>معرف المرجع من البوابة</summary>
    public static readonly TagKey Reference = new("payment_reference");

    /// <summary>معرف الطلب</summary>
    public static readonly TagKey Order = new("order_reference");

    /// <summary>نوع العملية. القيم: "initiate", "capture", "refund", "void"</summary>
    public static readonly TagKey Operation = new("pay_operation");

    /// <summary>دور الطرف. القيم: "customer", "merchant", "gateway"</summary>
    public static readonly TagKey Role = new("role");

    /// <summary>سبب الفشل</summary>
    public static readonly TagKey Reason = new("reason");
}

/// <summary>
/// هوية الطرف في عمليات الدفع.
/// </summary>
public sealed class PayPartyId
{
    public string Type { get; }
    public string Id { get; }
    public string FullId { get; }

    private PayPartyId(string type, string id)
    {
        Type = type; Id = id; FullId = $"{type}:{id}";
    }

    public static PayPartyId Customer(string customerId) => new("Customer", customerId);
    public static PayPartyId Merchant(string merchantId) => new("Merchant", merchantId);
    public static PayPartyId Gateway(string gateway) => new("Gateway", gateway);
    public static PayPartyId Order(string orderId) => new("Order", orderId);
    public static PayPartyId Payment(string paymentRef) => new("Payment", paymentRef);

    public override string ToString() => FullId;
    public static implicit operator string(PayPartyId pid) => pid.FullId;
}
