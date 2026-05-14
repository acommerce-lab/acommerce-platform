namespace Ashare.V3.Web.Payment;

/// <summary>
/// DelegatingHandler يَحقُن <c>X-Payment-Reference</c> عَلى كُلّ طَلَب
/// HTTP صادِر إذا <see cref="PaymentRequestContext.Reference"/> غَير فارِغ.
///
/// <para>نَفس نَمَط <c>CultureHeadersHandler</c> — الكيت لا يَعلَم،
/// التَطبيق يُسَجِّل الـ handler عَلى الـ HttpClient المَوحَّد ("ejar").
/// الـ backend interceptor عَلى <c>listing.create</c> يَقرَأ الـ header
/// ويَتَحَقَّق مَن صَلاحِيَّة الدَفع.</para>
/// </summary>
public sealed class PaymentReferenceHandler : DelegatingHandler
{
    private readonly PaymentRequestContext _ctx;
    public PaymentReferenceHandler(PaymentRequestContext ctx) => _ctx = ctx;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var reference = _ctx.Reference;
        if (!string.IsNullOrEmpty(reference))
        {
            request.Headers.Remove("X-Payment-Reference");
            request.Headers.Add("X-Payment-Reference", reference);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
