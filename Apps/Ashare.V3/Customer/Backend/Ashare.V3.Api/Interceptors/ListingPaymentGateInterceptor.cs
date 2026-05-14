using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using Ashare.V3.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Ashare.V3.Api.Interceptors;

/// <summary>
/// بَوّابَة دَفع لِـ <c>listing.create</c> — pre-phase:
/// تَتَحَقَّق فَقَط. لا تَستَهلِك حَتّى نَجاح الـ op.
///
/// <para><b>سُلوك</b>:</para>
/// <list type="number">
///   <item>يَقرَأ <c>X-Payment-Reference</c> مَن HTTP header.</item>
///   <item>يَبحَث في <c>ListingPayments</c> WHERE Reference + UserId.</item>
///   <item>يَفشَل إذا: مَفقود / status != captured / Consumed=true.</item>
///   <item>يُمَرِّر بِلا تَعديل. الاستِهلاك يَحدُث في
///         <see cref="ListingPaymentConsumeInterceptor"/> post-phase.</item>
/// </list>
/// </summary>
public sealed class ListingPaymentGateInterceptor : IOperationInterceptor
{
    private readonly IServiceProvider _services;
    private readonly IHttpContextAccessor _httpContext;

    public string Name => "ListingPaymentGate";
    public InterceptorPhase Phase => InterceptorPhase.Pre;

    public ListingPaymentGateInterceptor(IServiceProvider services, IHttpContextAccessor httpContext)
    {
        _services = services;
        _httpContext = httpContext;
    }

    public bool AppliesTo(Operation op) => op.Type == "listing.create";

    public async Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
    {
        var http = _httpContext.HttpContext;
        if (http is null) return AnalyzerResult.Fail("no_http_context");

        var paymentRef = http.Request.Headers["X-Payment-Reference"].FirstOrDefault();
        if (string.IsNullOrEmpty(paymentRef)) return AnalyzerResult.Fail("payment_required");

        var callerId = http.User.FindFirst("user_id")?.Value
                    ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(callerId)) return AnalyzerResult.Fail("not_authenticated");

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AshareV3DbContext>();
        var payment = await db.ListingPayments.AsNoTracking().FirstOrDefaultAsync(
            p => p.Reference == paymentRef && p.UserId == callerId, context.CancellationToken);

        if (payment is null)          return AnalyzerResult.Fail("payment_not_found");
        if (payment.Status != "captured") return AnalyzerResult.Fail($"payment_status_{payment.Status}");
        if (payment.Consumed)         return AnalyzerResult.Fail("payment_already_used");

        // الـ post-phase يَستَهلِك. هُنا لا نَكتُب شَيئاً.
        return AnalyzerResult.Pass();
    }
}

/// <summary>
/// Post-phase: يَستَهلِك الدَفع إذا الـ op نَجَح. فَصل المَنطِق عَن الـ Pre
/// يَضمَن أَنّ دَفعاً لا يُستَهلَك إلّا عِند نَجاح فِعلي — لَو analyzer
/// لاحِق فَشَل أَو حَفظ DB أَخفَق، المُستَخدِم يَحتَفِظ بِالدَفع لِمُحاوَلَة
/// ثانِيَة.
/// </summary>
public sealed class ListingPaymentConsumeInterceptor : IOperationInterceptor
{
    private readonly IServiceProvider _services;
    private readonly IHttpContextAccessor _httpContext;
    private readonly ILogger<ListingPaymentConsumeInterceptor> _logger;

    public string Name => "ListingPaymentConsume";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public ListingPaymentConsumeInterceptor(IServiceProvider services,
                                            IHttpContextAccessor httpContext,
                                            ILogger<ListingPaymentConsumeInterceptor> logger)
    {
        _services = services;
        _httpContext = httpContext;
        _logger = logger;
    }

    public bool AppliesTo(Operation op) => op.Type == "listing.create";

    public async Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
    {
        // الـ engine يَستَدعينا لِكُلّ post، لكِنّ نَتَجاهَل إن لَم يَنجَح.
        if (context.Operation.Status != OperationStatus.Completed) return AnalyzerResult.Pass();

        var http = _httpContext.HttpContext;
        var paymentRef = http?.Request.Headers["X-Payment-Reference"].FirstOrDefault();
        if (string.IsNullOrEmpty(paymentRef)) return AnalyzerResult.Pass();

        var callerId = http?.User.FindFirst("user_id")?.Value
                    ?? http?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(callerId)) return AnalyzerResult.Pass();

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AshareV3DbContext>();
        var payment = await db.ListingPayments.FirstOrDefaultAsync(
            p => p.Reference == paymentRef && p.UserId == callerId, context.CancellationToken);
        if (payment is null || payment.Consumed) return AnalyzerResult.Pass();

        payment.Consumed = true;
        payment.ListingId = TryExtractListingIdFromOp(context.Operation);
        payment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "ListingPaymentConsume: payment {Ref} consumed for user {U}",
            paymentRef, callerId);
        return AnalyzerResult.Pass();
    }

    private static Guid? TryExtractListingIdFromOp(Operation op)
    {
        var to = op.Parties.FirstOrDefault(p => p.Identity.StartsWith("Listing:", StringComparison.OrdinalIgnoreCase));
        if (to is null) return null;
        var idStr = to.Identity["Listing:".Length..];
        return Guid.TryParse(idStr, out var id) ? id : null;
    }
}
