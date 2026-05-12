using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using Ashare.V3.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Ashare.V3.Api.Interceptors;

/// <summary>
/// بَوّابَة دَفع لِـ <c>listing.create</c>: المُستَخدِم يَجِب أَن يَكون
/// قَد دَفَع رَسم نَشر إعلان قَبل أَن يُسمَح بِالعَمَلِيَّة. الاتِّساق
/// مَع نَمَط Subscriptions: عِندَما يَعود الكيت، يُضاف interceptor مُماثِل
/// يَفحَص اشتِراك المُستَخدِم — كِلاهُما يَحجِب الـ op pre-phase.
///
/// <para><b>سُلوك</b>:</para>
/// <list type="number">
///   <item>يَقرَأ <c>X-Payment-Reference</c> مَن HTTP header (الـ frontend
///         يَملَؤها بَعد ما /payments/listing/initiate يَنجَح).</item>
///   <item>يَبحَث في <c>ListingPayments</c> WHERE Reference == ref AND
///         UserId == caller AND Status == "captured" AND !Consumed.</item>
///   <item>إذا وُجِد: يَضَع علامَة Consumed=true، يُسَجِّل
///         <c>payment_reference</c> tag عَلى الـ op، يُمَرِّر.</item>
///   <item>إذا لَم يُوجَد: يَحجِب بِـ <c>payment_required</c>.</item>
/// </list>
///
/// <para><b>إعادَة الفَتح لِلباقات</b>: عِندَما يَعود
/// <c>Subscriptions</c> kit، أَضِف interceptor مُوازي يَفحَص اشتِراك
/// المُستَخدِم. تَكفي إضافَته بِـ <c>AddSingleton&lt;IOperationInterceptor, ...&gt;</c>
/// — لا تَعديل في الواجِهَة أَو الكيت Listings.</para>
/// </summary>
public sealed class ListingPaymentGateInterceptor : IOperationInterceptor
{
    private readonly IServiceProvider _services;
    private readonly IHttpContextAccessor _httpContext;
    private readonly ILogger<ListingPaymentGateInterceptor> _logger;

    public string Name => "ListingPaymentGate";
    public InterceptorPhase Phase => InterceptorPhase.Pre;

    public ListingPaymentGateInterceptor(
        IServiceProvider services,
        IHttpContextAccessor httpContext,
        ILogger<ListingPaymentGateInterceptor> logger)
    {
        _services = services;
        _httpContext = httpContext;
        _logger = logger;
    }

    /// <summary>
    /// ينطبق فَقَط عَلى <c>listing.create</c>. أَنواع أُخرى (toggle, update,
    /// my-listings list…) لا تَحتاج دَفعاً.
    /// </summary>
    public bool AppliesTo(Operation op) => op.Type == "listing.create";

    public async Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
    {
        var http = _httpContext.HttpContext;
        if (http is null)
            return AnalyzerResult.Fail("no_http_context");

        var paymentRef = http.Request.Headers["X-Payment-Reference"].FirstOrDefault();
        if (string.IsNullOrEmpty(paymentRef))
            return AnalyzerResult.Fail("payment_required");

        var callerId = http.User.FindFirst("user_id")?.Value
                    ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(callerId))
            return AnalyzerResult.Fail("not_authenticated");

        // Scope جَديد لِـ DbContext لِأَنّ الـ interceptor singleton (lifecycle
        // الكيت)، والـ DbContext scoped.
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AshareV3DbContext>();
        var payment = await db.ListingPayments.FirstOrDefaultAsync(
            p => p.Reference == paymentRef && p.UserId == callerId,
            context.CancellationToken);

        if (payment is null)
            return AnalyzerResult.Fail("payment_not_found");
        if (payment.Status != "captured")
            return AnalyzerResult.Fail($"payment_status_{payment.Status}");
        if (payment.Consumed)
            return AnalyzerResult.Fail("payment_already_used");

        // اِستَهلِك. الـ op يَستَمِرّ. لَو فَشَل الـ create لاحِقاً، الـ
        // SaveAtEnd لا يَلتَزِم — لكِنّ الـ Consumed=true الَّذي حَفَظناه
        // الآن يَبقى. هذا مَقصود: المُستَخدِم لا يُمكِنه إعادَة استِخدام
        // نَفس الدَفع، يَجِب أَن يَدفَع جَديداً.
        payment.Consumed = true;
        payment.UpdatedAt = DateTime.UtcNow;
        payment.ListingId = TryExtractListingIdFromOp(context.Operation);
        await db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "ListingPaymentGate: payment {Ref} consumed by user {U} for listing.create",
            paymentRef, callerId);
        return AnalyzerResult.Pass();
    }

    private static Guid? TryExtractListingIdFromOp(Operation op)
    {
        // الـ kit's create يَضَع To = "Listing:{guid}" ⇒ نَستَخلِص الـ guid.
        var to = op.Parties.FirstOrDefault(p => p.Identity.StartsWith("Listing:", StringComparison.OrdinalIgnoreCase));
        if (to is null) return null;
        var idStr = to.Identity["Listing:".Length..];
        return Guid.TryParse(idStr, out var id) ? id : null;
    }
}
