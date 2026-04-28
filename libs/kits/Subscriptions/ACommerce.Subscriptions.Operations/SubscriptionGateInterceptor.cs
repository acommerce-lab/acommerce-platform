using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.Subscriptions.Operations.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.Subscriptions.Operations;

/// <summary>
/// معترض بوّابة الاشتراكات الكونيّ — Pre، يحجب أيّ عمليّة تحمل تاج
/// <see cref="SubscriptionTagKeys.RequiresActiveSubscription"/> إذا لم يكن للمستخدم
/// أيّ اشتراك نشط على الإطلاق (بصرف النظر عن الحصص الفرديّة).
///
/// <para>الفرق عن <see cref="QuotaInterceptor"/>:
///   - <c>QuotaInterceptor</c> يربط القيد بحصّة محدّدة في باقة محدّدة (مثلاً
///     <c>listings.create &lt; max</c>) ويستهلك العدّاد.
///   - <c>SubscriptionGateInterceptor</c> ببساطة يفشل القيد لو ليس للمستخدم أيّ
///     اشتراك نشط — مفيد لعمليّات لا تتعلّق بحصّة (مثل قراءة dashboard أو
///     إرسال رسالة) لكنّها مقصورة على المشتركين.
/// </para>
///
/// <para>التطبيق يفعّله بإضافة التاج صراحةً:
/// <code>
///   Entry.Create("vendor.dashboard.read")
///       .Tag(SubscriptionTagKeys.RequiresActiveSubscription, "true")
///       ...
/// </code>
/// أو يطبّقه عالميّاً عبر TaggedInterceptor جانبيّ.</para>
/// </summary>
public sealed class SubscriptionGateInterceptor : IOperationInterceptor
{
    public string Name => nameof(SubscriptionGateInterceptor);
    public InterceptorPhase Phase => InterceptorPhase.Pre;

    public bool AppliesTo(Operation op)
    {
        if (op.HasTag(SubscriptionTagKeys.SkipSubscriptionGate)) return false;
        if (op.Type.StartsWith("subscription.", StringComparison.Ordinal)) return false;
        return op.HasTag(SubscriptionTagKeys.RequiresActiveSubscription);
    }

    public async Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
    {
        var userId = ResolveUserId(context);
        if (userId == Guid.Empty)
        {
            // لا نعرف المستخدم — نتركه لمعترض المصادقة، لا نضع قيداً مزدوجاً.
            return AnalyzerResult.Pass();
        }

        var provider = context.Services.GetService<ISubscriptionProvider>();
        if (provider is null)
        {
            // المكتبة غير مفعّلة في هذا الـ host — نمرّر.
            return AnalyzerResult.Pass();
        }

        var subs = await provider.GetActiveSubscriptionsAsync(userId, context.CancellationToken);
        var hasActive = subs.Any(s => s.IsCurrentlyActive);

        if (hasActive)
            return AnalyzerResult.Pass();

        var logger = context.Services.GetService<ILogger<SubscriptionGateInterceptor>>();
        logger?.LogInformation(
            "Subscription gate rejected operation {OpType} — user {UserId} has no active subscription",
            context.Operation.Type, userId);

        return new AnalyzerResult
        {
            Passed  = false,
            Message = SubscriptionTagKeys.RejectionCode_NoActiveSubscription,
            Data    = new Dictionary<string, object>
            {
                ["operation"] = context.Operation.Type,
                ["user_id"]   = userId
            }
        };
    }

    private static Guid ResolveUserId(OperationContext context)
    {
        // 1) من رأس HTTP أو claim للمصادَق عليه
        var http = context.Services.GetService<IHttpContextAccessor>()?.HttpContext;
        var claim = http?.User?.FindFirst("user_id")?.Value
                 ?? http?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var fromClaim))
            return fromClaim;

        // 2) من تاج صريح
        var explicitId = context.Operation.GetTagValue(QuotaTagKeys.UserId.Name);
        if (!string.IsNullOrEmpty(explicitId) && Guid.TryParse(explicitId, out var fromTag))
            return fromTag;

        // 3) من طرف صاحب الدور
        var roles = new[] { "owner", "subject", "subscriber", "customer" };
        foreach (var role in roles)
        {
            var party = context.Operation.GetPartiesByTag("role", role).FirstOrDefault();
            if (party is null) continue;
            var ix = party.Identity.IndexOf(':');
            if (ix > 0 && Guid.TryParse(party.Identity[(ix + 1)..], out var fromParty))
                return fromParty;
        }

        return Guid.Empty;
    }
}
