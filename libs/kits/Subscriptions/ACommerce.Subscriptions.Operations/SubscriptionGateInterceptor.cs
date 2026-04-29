using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.Subscriptions.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.Subscriptions.Operations;

/// <summary>
/// معترض بوّابة الاشتراكات الكونيّ — Pre، يحجب أيّ عمليّة تحمل تاج
/// <see cref="SubscriptionTagKeys.RequiresActiveSubscription"/> إذا لم يكن للمستخدم
/// أيّ اشتراك نشط على الإطلاق.
///
/// <para>الفرق عن <see cref="QuotaInterceptor"/>:
///   - <c>QuotaInterceptor</c> يربط القيد بحصّة محدّدة في باقة محدّدة.
///   - <c>SubscriptionGateInterceptor</c> ببساطة يفشل القيد لو ليس للمستخدم أيّ
///     اشتراك نشط — مفيد لعمليّات لا تتعلّق بحصّة لكنّها مقصورة على المشتركين.
/// </para>
///
/// <para>التطبيق يفعّله بإضافة التاج صراحةً:
/// <code>
///   Entry.Create("vendor.dashboard.read")
///       .Tag(SubscriptionTagKeys.RequiresActiveSubscription, "true")
///       .Tag(QuotaTagKeys.UserId.Name, userId.ToString())
///       ...
/// </code>
/// </para>
///
/// <para>يعتمد فقط على تاجات/أطراف العمليّة (لا <c>HttpContext</c>) ليعمل في WASM
/// وفي background jobs بنفس السلوك. إن لم نتمكّن من تحديد المستخدم، نمرّر بسلام
/// (يتولّى معترض المصادقة الرفض).</para>
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
        var userId = ResolveUserId(context.Operation);
        if (userId == Guid.Empty)
            return AnalyzerResult.Pass();

        var provider = context.Services.GetService<ISubscriptionProvider>();
        if (provider is null)
            return AnalyzerResult.Pass();

        var subs = await provider.GetActiveSubscriptionsAsync(userId, context.CancellationToken);
        if (subs.Any(s => s.IsCurrentlyActive))
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

    /// <summary>
    /// نفس استراتيجية <see cref="QuotaInterceptor"/>: تاج صريح أوّلاً، ثمّ طرف
    /// بدور owner/subject/subscriber/customer/sender. لا اعتماد على HttpContext
    /// — يحفظ التوافق مع WASM والـ background jobs.
    /// </summary>
    private static Guid ResolveUserId(Operation op)
    {
        var explicitId = op.GetTagValue(QuotaTagKeys.UserId.Name);
        if (!string.IsNullOrEmpty(explicitId) && Guid.TryParse(explicitId, out var fromTag))
            return fromTag;

        var roles = new[] { "owner", "subject", "subscriber", "customer", "sender" };
        foreach (var role in roles)
        {
            var party = op.GetPartiesByTag("role", role).FirstOrDefault();
            if (party is null) continue;
            var ix = party.Identity.IndexOf(':');
            if (ix > 0 && Guid.TryParse(party.Identity[(ix + 1)..], out var fromParty))
                return fromParty;
        }
        return Guid.Empty;
    }
}
