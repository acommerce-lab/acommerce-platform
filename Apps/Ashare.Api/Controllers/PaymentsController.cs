using ACommerce.OperationEngine.Core;
using ACommerce.Payments.Operations;
using ACommerce.Payments.Operations.Abstractions;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using PaymentEntity = Ashare.Api.Entities.Payment;
using PaymentEntityStatus = Ashare.Api.Entities.PaymentStatus;
using LibPaymentStatus = ACommerce.Payments.Operations.Abstractions.PaymentStatus;

namespace Ashare.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentService _payments;
    private readonly IBaseAsyncRepository<PaymentEntity> _payRepo;
    private readonly IBaseAsyncRepository<Booking> _bookings;
    private readonly OpEngine _engine;

    public PaymentsController(
        PaymentService payments,
        IRepositoryFactory factory,
        OpEngine engine)
    {
        _payments = payments;
        _payRepo  = factory.CreateRepository<PaymentEntity>();
        _bookings = factory.CreateRepository<Booking>();
        _engine   = engine;
    }

    public record InitiatePaymentRequest(Guid BookingId);

    /// <summary>
    /// بدء عملية دفع لحجز عبر بوابة Noon.
    /// تستخدم PaymentOps.Initiate (قيد محاسبي: العميل ← البوابة).
    /// </summary>
    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate([FromBody] InitiatePaymentRequest req, CancellationToken ct)
    {
        var booking = await _bookings.GetByIdAsync(req.BookingId, ct);
        if (booking == null) return this.NotFoundEnvelope("booking_not_found");

        if (booking.Status == BookingStatus.Paid)
            return this.BadRequestEnvelope("already_paid");

        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            BookingId = booking.Id,
            CustomerId = booking.CustomerId,
            Gateway = "noon",
            Amount = booking.TotalPrice,
            Currency = booking.Currency,
            Status = PaymentEntityStatus.Pending
        };
        await _payRepo.AddAsync(payment, ct);

        // ─── استخدام مكتبة الدفع المحاسبية ───
        var pr = new PaymentRequest(
            Amount: booking.TotalPrice,
            Currency: booking.Currency,
            OrderReference: booking.Id.ToString(),
            CustomerId: booking.CustomerId.ToString(),
            Description: $"Booking #{booking.Id}",
            ReturnUrl: $"https://ashare.test/payments/return?paymentId={payment.Id}",
            WebhookUrl: $"https://ashare.test/api/payments/webhook/noon");

        var outcome = await _payments.InitiateAsync("noon", booking.CustomerId.ToString(), pr, ct);

        if (!outcome.Succeeded)
        {
            payment.Status = PaymentEntityStatus.Failed;
            payment.FailureReason = outcome.Error;
            await _payRepo.UpdateAsync(payment, ct);
            return this.BadRequestEnvelope("payment_initiate_failed", outcome.Error);
        }

        payment.GatewayReference = outcome.PaymentReference;
        payment.PaymentUrl = outcome.PaymentUrl;
        payment.Status = PaymentEntityStatus.Authorized;
        await _payRepo.UpdateAsync(payment, ct);

        return this.OkEnvelope("payment.initiate", payment);
    }

    /// <summary>
    /// callback من Noon (تأكيد الدفع). محاكاة فقط في الوضع التجريبي.
    /// في الإنتاج: يتحقق من توقيع HMAC ويحدّث الحالة.
    /// </summary>
    [HttpPost("callback/{paymentId:guid}")]
    public async Task<IActionResult> Callback(Guid paymentId, [FromQuery] bool success = true, CancellationToken ct = default)
    {
        var payment = await _payRepo.GetByIdAsync(paymentId, ct);
        if (payment == null) return this.NotFoundEnvelope("payment_not_found");

        if (success)
        {
            payment.Status = PaymentEntityStatus.Captured;
            payment.PaidAt = DateTime.UtcNow;
        }
        else
        {
            payment.Status = PaymentEntityStatus.Failed;
            payment.FailureReason = "callback_failure";
        }
        await _payRepo.UpdateAsync(payment, ct);

        // تحديث حالة الحجز
        if (payment.BookingId.HasValue)
        {
            var booking = await _bookings.GetByIdAsync(payment.BookingId.Value, ct);
            if (booking != null)
            {
                booking.Status = success ? BookingStatus.Paid : BookingStatus.AwaitingPayment;
                booking.PaymentId = payment.Id;
                await _bookings.UpdateAsync(booking, ct);
            }
        }

        return this.OkEnvelope("payment.callback", payment);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var p = await _payRepo.GetByIdAsync(id, ct);
        return p == null ? this.NotFoundEnvelope("payment_not_found") : this.OkEnvelope("payment.get", p);
    }

    [HttpPost("{id:guid}/refund")]
    public async Task<IActionResult> Refund(Guid id, [FromQuery] decimal? amount, CancellationToken ct)
    {
        var payment = await _payRepo.GetByIdAsync(id, ct);
        if (payment == null) return this.NotFoundEnvelope("payment_not_found");
        if (payment.Status != PaymentEntityStatus.Captured)
            return this.BadRequestEnvelope("cannot_refund_uncaptured");

        var refundAmount = amount ?? payment.Amount;
        var outcome = await _payments.RefundAsync(
            "noon",
            merchantId: "ashare-merchant",
            customerId: payment.CustomerId.ToString(),
            paymentReference: payment.GatewayReference ?? payment.Id.ToString(),
            amount: refundAmount,
            currency: payment.Currency,
            reason: "customer_request",
            ct: ct);

        if (!outcome.Succeeded)
            return this.BadRequestEnvelope("refund_failed", outcome.Error);

        payment.Status = PaymentEntityStatus.Refunded;
        payment.RefundedAmount = refundAmount;
        payment.RefundedAt = DateTime.UtcNow;
        await _payRepo.UpdateAsync(payment, ct);

        return this.OkEnvelope("payment.refund", payment);
    }
}
