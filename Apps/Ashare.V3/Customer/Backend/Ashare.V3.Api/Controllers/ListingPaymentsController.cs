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
/// نُقَط الدَفع لِنَشر إعلان في V3. الـ frontend يَستَدعي
/// <c>POST /payments/listing/initiate</c> قَبل ما يَنشُر، يَتَلَقّى
/// <c>{reference, paymentUrl}</c>، يَنتَظِر اكتِمال الدَفع (mock يَنجَح
/// تِلقائيّاً بَعد ثَوانٍ — production يَستَخدِم webhook)، ثُمّ يُرسِل
/// <c>POST /my-listings</c> مَع header <c>X-Payment-Reference: {ref}</c>
/// — <see cref="Interceptors.ListingPaymentGateInterceptor"/> يَفحَصه.
///
/// <para>سِعر الإعلان: ثابِت مِن أَجل MVP، قابِل لِلتَكوين عَبر
/// <c>appsettings:Payments:ListingPrice</c>. مَكان مُلائِم لاحِقاً
/// لِتَسعير حَسَب الفِئَة أَو الإبراز.</para>
/// </summary>
[ApiController]
[Authorize]
public sealed class ListingPaymentsController : ControllerBase
{
    private readonly AshareV3DbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly IConfiguration _config;
    public ListingPaymentsController(AshareV3DbContext db, IPaymentGateway gateway, IConfiguration config)
    {
        _db = db;
        _gateway = gateway;
        _config = config;
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

        _db.ListingPayments.Add(new ListingPaymentEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UserId = CallerId,
            Provider = _gateway.Name,
            Reference = init.PaymentReference,
            Amount = amount,
            Currency = currency,
            Status = "pending",
        });
        await _db.SaveChangesAsync(ct);

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
            payment.Status = effective;
            if (effective == "captured") payment.CapturedAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
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
    /// Webhook عامّ لِكُلّ المُزَوِّدين. الـ gateway يَستَدعيها بِـ rawBody +
    /// headers ⇒ <see cref="IPaymentGateway.ParseWebhookAsync"/> يَتَحَقَّق
    /// مَن التَوقيع ويُرجِع الحالَة الجَديدَة. نُحَدِّث الـ DB row فَوراً.
    /// Mock يَستَقبِل: <c>{"ref":"…","status":"captured"}</c>.
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
        if (payment.Status != newStatus)
        {
            payment.Status = newStatus;
            if (newStatus == "captured") payment.CapturedAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return this.OkEnvelope("payment.webhook", new { ok = true, status = newStatus });
    }
}
