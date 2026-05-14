using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.Payments.Operations.Abstractions;
using Ashare.V3.Data;
using Ashare.V3.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ashare.V3.Api.Controllers;

/// <summary>
/// نُقَط الدَفع لِنَشر إعلان في V3. كُلّ مَسار هُنا الآن يَمُرّ عَبر
/// <see cref="OpEngine"/> + <c>SaveAtEnd</c> ⇒ التَدقيق + Idempotency +
/// كُلّ interceptors الـ post-write تَنطَبِق تِلقائيّاً. <b>لا
/// <c>SaveChangesAsync</c> مُباشِر هُنا</b>.
///
/// <para>تَدَفُّق نَموذجي:</para>
/// <list type="number">
///   <item>Frontend ⇒ <c>POST /payments/listing/initiate</c> ⇒ op
///         <c>payment.listing.initiate</c> يُسَجِّل صَفّ pending.</item>
///   <item>Frontend ⇒ polls <c>/status</c> أَو الـ gateway يُرسِل webhook ⇒
///         op <c>payment.listing.status</c> أَو <c>payment.webhook.{provider}</c>
///         يُرَقّي الحالَة.</item>
///   <item>Frontend ⇒ <c>POST /my-listings</c> مَع
///         <c>X-Payment-Reference</c> ⇒ ListingPaymentGateInterceptor
///         يَسمَح، ListingPaymentConsumeInterceptor يَضَع
///         <c>Consumed = true</c> عِندَ نَجاح إنشاء الإعلان.</item>
/// </list>
/// </summary>
[ApiController]
[Authorize]
public sealed class ListingPaymentsController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    private readonly IPaymentGateway   _gateway;
    private readonly IConfiguration    _config;
    private readonly OpEngine          _engine;
    public ListingPaymentsController(
        AshareV3DbContext db, IPaymentGateway gateway,
        IConfiguration config, OpEngine engine)
    {
        _db = db;
        _gateway = gateway;
        _config = config;
        _engine = engine;
    }

    private string? CallerId =>
        User.FindFirst("user_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public sealed record InitiateBody(decimal? Amount, string? Currency);

    [HttpPost("/payments/listing/initiate")]
    public async Task<IActionResult> Initiate([FromBody] InitiateBody? body, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();

        var amount = body?.Amount
                     ?? _config.GetValue<decimal?>("Payments:ListingPrice")
                     ?? 5_000m;
        var currency = body?.Currency
                       ?? _config["Payments:Currency"]
                       ?? "SAR";

        var orderRef = "listing_" + Guid.NewGuid().ToString("N")[..12];
        var init = await _gateway.InitiateAsync(new PaymentRequest(
            Amount:         amount,
            Currency:       currency,
            OrderReference: orderRef,
            CustomerId:     CallerId,
            Description:    "رسم نشر إعلان"), ct);

        if (!init.Succeeded || init.PaymentReference is null)
            return this.BadRequestEnvelope("payment_initiate_failed", init.Error);

        var paymentId = Guid.NewGuid();
        var op = Entry.Create("payment.listing.initiate")
            .Describe($"User {CallerId} initiates listing payment {init.PaymentReference}")
            .From($"User:{CallerId}",        1, ("role", "payer"))
            .To($"Payment:{init.PaymentReference}", 1, ("role", "created"))
            .Tag("user_id",          CallerId)
            .Tag("provider",         _gateway.Name)
            .Tag("reference",        init.PaymentReference)
            .Tag("amount",           amount.ToString())
            .Tag("currency",         currency)
            .Execute(ctx =>
            {
                _db.ListingPayments.Add(new ListingPaymentEntity
                {
                    Id        = paymentId,
                    CreatedAt = DateTime.UtcNow,
                    UserId    = CallerId,
                    Provider  = _gateway.Name,
                    Reference = init.PaymentReference,
                    Amount    = amount,
                    Currency  = currency,
                    Status    = "pending",
                });
                return Task.CompletedTask;
            })
            .SaveAtEnd()
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { reference = init.PaymentReference }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "initiate_failed",
                                           env.Operation.ErrorMessage);

        return this.OkEnvelope("payment.listing.initiate", new
        {
            reference  = init.PaymentReference,
            paymentUrl = init.PaymentUrl,
            amount,
            currency,
            providerData = init.ProviderData,
        });
    }

    /// <summary>
    /// استِعلام حالَة. الـ frontend يَستَدعيها كُلّ بِضع ثَوانٍ بَعد فَتح
    /// صَفحَة الدَفع. عِندَ <c>captured</c> ⇒ يَنتَقِل لِإرسال الإعلان مَع
    /// <c>X-Payment-Reference</c>.
    /// </summary>
    [HttpGet("/payments/listing/{reference}/status")]
    public async Task<IActionResult> Status(string reference, CancellationToken ct)
    {
        if (CallerId is null) return this.UnauthorizedEnvelope();

        var payment = await _db.ListingPayments.FirstOrDefaultAsync(
            p => p.Reference == reference && p.UserId == CallerId, ct);
        if (payment is null) return this.NotFoundEnvelope("payment_not_found");

        // اِسأَل الـ gateway لِحالَة فِعليَّة (mock يُرَقّي Authorized→Captured
        // بَعد AutoCaptureSeconds).
        var status = await _gateway.GetStatusAsync(reference, ct);
        var effective = status.Succeeded ? status.Status.ToString() : payment.Status;

        if (status.Succeeded && payment.Status != effective)
        {
            // التَّحديث عَبر op لِتَدقيق التَغيير + Idempotency.
            var op = Entry.Create("payment.listing.status")
                .Describe($"Payment {reference} status: {payment.Status} → {effective}")
                .From($"User:{CallerId}",                1, ("role", "poller"))
                .To($"Payment:{reference}",              1, ("role", "updated"))
                .Tag("reference",  reference)
                .Tag("from",       payment.Status)
                .Tag("to",         effective)
                .Execute(ctx =>
                {
                    payment.Status    = effective;
                    if (effective == "captured") payment.CapturedAt = DateTime.UtcNow;
                    payment.UpdatedAt = DateTime.UtcNow;
                    return Task.CompletedTask;
                })
                .SaveAtEnd()
                .Build();
            await _engine.ExecuteEnvelopeAsync(op, new { reference }, ct);
        }

        return this.OkEnvelope("payment.listing.status", new
        {
            reference  = payment.Reference,
            status     = payment.Status,
            consumed   = payment.Consumed,
            amount     = payment.Amount,
        });
    }

    /// <summary>
    /// Webhook عامّ لِكُلّ المُزَوِّدين. <b>طَلَب أَنونيموس</b> — الـ gateway
    /// يَتَوَقَّع endpoint مَفتوح. الحِمايَة تَأتي مَن:
    /// <list type="bullet">
    ///   <item><see cref="IPaymentGateway.ParseWebhookAsync"/> يَتَحَقَّق مَن
    ///         التَوقيع (HMAC/signature المُزَوِّد).</item>
    ///   <item>الـ op يَحوي <c>idempotency_key = "webhook:" + reference + ":" + status</c>
    ///         ⇒ Idempotency interceptor يَرفُض إعادَة المُحاوَلَة بِنَفس
    ///         الحالَة (المُزَوِّدون يُكَرِّرون webhooks عادَةً).</item>
    /// </list>
    /// </summary>
    [HttpPost("/payments/webhook/{provider}")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(string provider, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var raw = await reader.ReadToEndAsync(ct);
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());

        var parsed = await _gateway.ParseWebhookAsync(raw, headers, ct);
        if (!parsed.IsValid || string.IsNullOrEmpty(parsed.PaymentReference))
            return this.BadRequestEnvelope("invalid_webhook", parsed.Error);

        var payment = await _db.ListingPayments.FirstOrDefaultAsync(
            p => p.Reference == parsed.PaymentReference, ct);
        if (payment is null) return this.NotFoundEnvelope("payment_not_found");

        var newStatus = parsed.Status?.ToString() ?? payment.Status;

        // الـ op يَحمِل idempotency_key مَبني مَن reference+status ⇒ نَفس
        // webhook لِنَفس الحالَة لا يُنَفَّذ مَرَّتَين (المُزَوِّدون يُعيدون
        // إرسال webhooks عِندَ retry). الـ IdempotencyInterceptor يَفحَص.
        var idKey = $"webhook:{provider}:{parsed.PaymentReference}:{newStatus}";
        var op = Entry.Create("payment.webhook")
            .Describe($"Webhook {provider} for {parsed.PaymentReference}: {payment.Status} → {newStatus}")
            .From($"PaymentProvider:{provider}", 1, ("role", "webhook"))
            .To($"Payment:{parsed.PaymentReference}", 1, ("role", "updated"))
            .Tag("provider",        provider)
            .Tag("reference",       parsed.PaymentReference)
            .Tag("from",            payment.Status)
            .Tag("to",              newStatus)
            .Tag("idempotency_key", idKey)
            .Execute(ctx =>
            {
                if (payment.Status != newStatus)
                {
                    payment.Status = newStatus;
                    if (newStatus == "captured") payment.CapturedAt = DateTime.UtcNow;
                    payment.UpdatedAt = DateTime.UtcNow;
                }
                return Task.CompletedTask;
            })
            .SaveAtEnd()
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { reference = parsed.PaymentReference, status = newStatus }, ct);
        if (env.Operation.Status != "Success" && env.Operation.FailedAnalyzer != "idempotent_replay")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "webhook_failed", env.Operation.ErrorMessage);

        return this.OkEnvelope("payment.webhook", new { ok = true, status = newStatus });
    }
}
