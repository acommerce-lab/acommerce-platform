using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.Payments.Operations.Abstractions;

namespace ACommerce.Payments.Operations.Operations;

/// <summary>
/// قيود الدفع - كل تحويل مالي = قيد بين أطراف بمبلغ متوازن.
///
/// Initiate: Customer (مدين) → Gateway (دائن) بالمبلغ.
/// Capture: Gateway (مدين) → Merchant (دائن).
/// Refund: Merchant (مدين) → Customer (دائن).
/// Void: إلغاء تفويض لم يُلتقط.
/// </summary>
public static class PaymentOps
{
    /// <summary>
    /// قيد: بدء دفع.
    /// العميل (مدين) يُصدر دفعاً → البوابة (دائن) تستلمه.
    /// </summary>
    public static Operation Initiate(
        PayPartyId customer,
        PaymentRequest request,
        IPaymentGateway gateway)
    {
        var currency = request.Currency;
        var amount = request.Amount;

        return Entry.Create("payment.initiate")
            .Describe($"{customer} initiates payment {amount} {currency}")
            .From(customer, amount,
                (PayTags.Role, "customer"),
                (PayTags.Status, PaymentStatus.Pending))
            .To(PayPartyId.Gateway(gateway.Name), amount,
                (PayTags.Role, "gateway"),
                (PayTags.Status, PaymentStatus.Pending))
            .Tag(PayTags.Gateway, gateway.Name)
            .Tag(PayTags.Currency, currency)
            .Tag(PayTags.Order, request.OrderReference)
            .Tag(PayTags.Operation, "initiate")
            .Execute(async ctx =>
            {
                var result = await gateway.InitiateAsync(request, ctx.CancellationToken);

                var gatewayParty = ctx.Operation.GetPartiesByTag(PayTags.Role, "gateway").FirstOrDefault();

                if (!result.Succeeded)
                {
                    if (gatewayParty != null)
                    {
                        gatewayParty.RemoveTag(PayTags.Status);
                        gatewayParty.AddTag(PayTags.Status, PaymentStatus.Failed);
                    }
                    ctx.Set("error", result.Error ?? "initiate_failed");
                    throw new PaymentException(result.Error ?? "Failed to initiate payment");
                }

                if (gatewayParty != null && !string.IsNullOrEmpty(result.PaymentReference))
                {
                    gatewayParty.AddTag(PayTags.Reference, result.PaymentReference);
                }

                ctx.Set("paymentReference", result.PaymentReference);
                ctx.Set("paymentUrl", result.PaymentUrl);
                ctx.Set("providerData", result.ProviderData);
            })
            .Build();
    }

    /// <summary>
    /// قيد: التقاط دفع مُفوّض.
    /// البوابة (مدين) تُحوّل → التاجر (دائن) يستلم.
    /// </summary>
    public static Operation Capture(
        PayPartyId merchant,
        string paymentReference,
        decimal amount,
        string currency,
        IPaymentGateway gateway,
        Guid? originalOpId = null)
    {
        var builder = Entry.Create("payment.capture")
            .Describe($"Capture {paymentReference}: {amount} {currency}")
            .From(PayPartyId.Gateway(gateway.Name), amount,
                (PayTags.Role, "gateway"),
                (PayTags.Reference, paymentReference))
            .To(merchant, amount,
                (PayTags.Role, "merchant"),
                (PayTags.Status, PaymentStatus.Captured))
            .Tag(PayTags.Gateway, gateway.Name)
            .Tag(PayTags.Currency, currency)
            .Tag(PayTags.Reference, paymentReference)
            .Tag(PayTags.Operation, "capture")
            .Execute(async ctx =>
            {
                var result = await gateway.CaptureAsync(paymentReference, amount, ctx.CancellationToken);

                if (!result.Succeeded)
                {
                    ctx.Set("error", result.Error ?? "capture_failed");
                    throw new PaymentException(result.Error ?? "Failed to capture payment");
                }

                ctx.Set("transactionId", result.TransactionId);
                ctx.Set("capturedAmount", result.CapturedAmount);
            });

        if (originalOpId.HasValue)
            builder.Fulfills(originalOpId.Value);

        return builder.Build();
    }

    /// <summary>
    /// قيد: استرداد.
    /// التاجر (مدين) يُعيد → العميل (دائن) يستلم.
    /// </summary>
    public static Operation Refund(
        PayPartyId merchant,
        PayPartyId customer,
        string paymentReference,
        decimal amount,
        string currency,
        IPaymentGateway gateway,
        string? reason = null,
        Guid? originalOpId = null)
    {
        var builder = Entry.Create("payment.refund")
            .Describe($"Refund {paymentReference}: {amount} {currency}")
            .From(merchant, amount,
                (PayTags.Role, "merchant"))
            .To(customer, amount,
                (PayTags.Role, "customer"),
                (PayTags.Status, PaymentStatus.Refunded))
            .Tag(PayTags.Gateway, gateway.Name)
            .Tag(PayTags.Currency, currency)
            .Tag(PayTags.Reference, paymentReference)
            .Tag(PayTags.Operation, "refund");

        if (!string.IsNullOrEmpty(reason))
            builder.Tag(PayTags.Reason, reason);

        builder.Execute(async ctx =>
        {
            var result = await gateway.RefundAsync(paymentReference, amount, reason, ctx.CancellationToken);

            if (!result.Succeeded)
            {
                ctx.Set("error", result.Error ?? "refund_failed");
                throw new PaymentException(result.Error ?? "Failed to refund payment");
            }

            ctx.Set("refundId", result.RefundId);
            ctx.Set("refundedAmount", result.RefundedAmount);
        });

        if (originalOpId.HasValue)
            builder.Reverses(originalOpId.Value);

        return builder.Build();
    }

    /// <summary>
    /// قيد: إلغاء تفويض لم يُلتقط.
    /// </summary>
    public static Operation Void(
        string paymentReference,
        IPaymentGateway gateway,
        Guid? originalOpId = null)
    {
        var builder = Entry.Create("payment.void")
            .Describe($"Void {paymentReference}")
            .From(PayPartyId.Gateway(gateway.Name), 1,
                (PayTags.Role, "gateway"),
                (PayTags.Status, PaymentStatus.Voided))
            .To(PayPartyId.Payment(paymentReference), 1,
                (PayTags.Status, PaymentStatus.Cancelled))
            .Tag(PayTags.Gateway, gateway.Name)
            .Tag(PayTags.Reference, paymentReference)
            .Tag(PayTags.Operation, "void")
            .Execute(async ctx =>
            {
                var result = await gateway.VoidAsync(paymentReference, ctx.CancellationToken);

                if (!result.Succeeded)
                {
                    ctx.Set("error", result.Error ?? "void_failed");
                    throw new PaymentException(result.Error ?? "Failed to void payment");
                }
            });

        if (originalOpId.HasValue)
            builder.Reverses(originalOpId.Value);

        return builder.Build();
    }

    /// <summary>
    /// قيد: استعلام عن حالة.
    /// لا قيمة مالية - مجرد استعلام.
    /// </summary>
    public static Operation Query(
        string paymentReference,
        IPaymentGateway gateway)
    {
        return Entry.Create("payment.query")
            .Describe($"Query status of {paymentReference}")
            .From(PayPartyId.Gateway(gateway.Name), 1, (PayTags.Role, "gateway"))
            .To(PayPartyId.Payment(paymentReference), 1)
            .Tag(PayTags.Gateway, gateway.Name)
            .Tag(PayTags.Reference, paymentReference)
            .Tag(PayTags.Operation, "query")
            .Execute(async ctx =>
            {
                var result = await gateway.GetStatusAsync(paymentReference, ctx.CancellationToken);
                ctx.Set("status", result.Status);
                ctx.Set("amount", result.Amount);
            })
            .Build();
    }
}
