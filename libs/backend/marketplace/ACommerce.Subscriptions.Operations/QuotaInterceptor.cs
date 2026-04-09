using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.Subscriptions.Operations.Abstractions;
using Microsoft.Extensions.Logging;

namespace ACommerce.Subscriptions.Operations;

/// <summary>
/// معترض حصص الباقة (PreInterceptor) - يعمل قبل تنفيذ أي قيد عليه علامة "quota_check".
///
/// FIFO: يجد أقدم اشتراك نشط للمستخدم له حصة متبقية مطابقة للنوع (والنطاق إن وُجد).
/// يفشل القيد إذا لم يكن هناك اشتراك مناسب.
/// عند النجاح يضع linked_subscription + linked_plan + quota_key في الـ context
/// ليستخدمها QuotaConsumptionInterceptor لاحقاً.
///
/// مفتاح العلامات في القيد:
///   - "quota_check" → اسم نوع الحصة (مثل: "listings.create" أو "messages.send")
///   - "quota_user_id" → معرّف المستخدم (يستخرجه أيضاً من party.role=owner/sender/subject)
///   - "quota_scope_key" → مفتاح النطاق (مثل: "listing_categories")
///   - "quota_scope_value" → قيمة النطاق (مثل: "residential")
/// </summary>
public class QuotaInterceptor : IOperationInterceptor
{
    private readonly ISubscriptionProvider _provider;
    private readonly ILogger<QuotaInterceptor> _logger;

    public string Name => "QuotaInterceptor";
    public InterceptorPhase Phase => InterceptorPhase.Pre;

    public QuotaInterceptor(ISubscriptionProvider provider, ILogger<QuotaInterceptor> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public bool AppliesTo(Operation op) => op.HasTag(QuotaTagKeys.Check.Name);

    public async Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
    {
        var op = context.Operation;
        var quotaKey = op.GetTagValue(QuotaTagKeys.Check.Name);
        if (string.IsNullOrEmpty(quotaKey))
            return AnalyzerResult.Fail("quota_check_tag_missing");

        var userId = ResolveUserId(op);
        if (userId == Guid.Empty)
            return AnalyzerResult.Fail("quota_user_not_resolved");

        var scopeKey = op.GetTagValue(QuotaTagKeys.ScopeKey.Name);
        var scopeValue = op.GetTagValue(QuotaTagKeys.ScopeValue.Name);

        var subs = await _provider.GetActiveSubscriptionsAsync(userId, context.CancellationToken);
        var ordered = subs.Where(s => s.IsCurrentlyActive).OrderBy(s => s.StartDate).ToList();

        if (ordered.Count == 0)
            return AnalyzerResult.Fail("no_active_subscription");

        // FIFO على الاشتراكات
        foreach (var sub in ordered)
        {
            var plan = await _provider.GetPlanAsync(sub.PlanId, context.CancellationToken);
            if (plan == null) continue;

            // فحص النطاق (مثلاً: الفئة)
            if (!string.IsNullOrEmpty(scopeKey) && !string.IsNullOrEmpty(scopeValue))
            {
                if (plan.AllowedScopes.TryGetValue(scopeKey, out var allowedCsv) && !string.IsNullOrWhiteSpace(allowedCsv))
                {
                    var allowed = allowedCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (!allowed.Contains(scopeValue, StringComparer.OrdinalIgnoreCase))
                        continue;
                }
            }

            // فحص الحصة
            if (!plan.Quotas.TryGetValue(quotaKey, out var max))
            {
                // لا توجد حصة مُعرّفة لهذا النوع في هذه الباقة - تخطّي
                continue;
            }

            if (max == -1)
            {
                // غير محدود - نجح
                BindToContext(context, sub, plan, quotaKey, max, used: 0);
                return AnalyzerResult.Pass();
            }

            var used = sub.Used.GetValueOrDefault(quotaKey, 0);
            if (used >= max)
                continue;  // لا رصيد - جرّب الاشتراك التالي

            // وجدنا اشتراكاً صالحاً
            BindToContext(context, sub, plan, quotaKey, max, used);

            return new AnalyzerResult
            {
                Passed = true,
                Message = $"linked_to_{plan.Slug}",
                Data = new Dictionary<string, object>
                {
                    ["subscription_id"] = sub.Id,
                    ["plan_id"] = plan.Id,
                    ["plan_slug"] = plan.Slug,
                    ["quota_key"] = quotaKey,
                    ["used"] = used,
                    ["max"] = max,
                    ["remaining_after"] = max - used - 1
                }
            };
        }

        return AnalyzerResult.Fail($"no_subscription_with_quota: {quotaKey}");
    }

    private static void BindToContext(OperationContext ctx, ISubscription sub, IPlan plan, string quotaKey, int max, int used)
    {
        ctx.Set("linked_subscription", sub);
        ctx.Set("linked_plan", plan);
        ctx.Set("quota_key", quotaKey);
        ctx.Set("quota_used_before", used);
        ctx.Set("quota_max", max);
    }

    private static Guid ResolveUserId(Operation op)
    {
        // 1) من علامة صريحة
        var explicitId = op.GetTagValue(QuotaTagKeys.UserId.Name);
        if (!string.IsNullOrEmpty(explicitId) && Guid.TryParse(explicitId, out var id))
            return id;

        // 2) من طرف بدور owner/sender/subject/customer
        var roles = new[] { "owner", "sender", "subject", "customer", "subscriber" };
        foreach (var role in roles)
        {
            var party = op.GetPartiesByTag("role", role).FirstOrDefault();
            if (party != null)
            {
                // Identity = "User:guid"
                var identity = party.Identity;
                var colonIdx = identity.IndexOf(':');
                if (colonIdx > 0 && Guid.TryParse(identity[(colonIdx + 1)..], out var partyId))
                    return partyId;
            }
        }

        return Guid.Empty;
    }
}

/// <summary>
/// معترض استهلاك الحصة (PostInterceptor) - يزيد العدّاد بعد نجاح التنفيذ.
/// يقرأ من الـ context القيم التي وضعها QuotaInterceptor قبله.
/// </summary>
public class QuotaConsumptionInterceptor : IOperationInterceptor
{
    private readonly ISubscriptionProvider _provider;
    private readonly ILogger<QuotaConsumptionInterceptor> _logger;

    public string Name => "QuotaConsumptionInterceptor";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    public QuotaConsumptionInterceptor(ISubscriptionProvider provider, ILogger<QuotaConsumptionInterceptor> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public bool AppliesTo(Operation op) => op.HasTag(QuotaTagKeys.Check.Name);

    public async Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
    {
        if (!context.TryGet<ISubscription>("linked_subscription", out var sub) || sub == null)
            return AnalyzerResult.Warning("no_linked_subscription");

        if (!context.TryGet<string>("quota_key", out var quotaKey) || string.IsNullOrEmpty(quotaKey))
            return AnalyzerResult.Warning("no_quota_key");

        // زيادة العدّاد - نسخة dict + إعادة إسناد كاملة (يستدعي setter في التطبيقات
        // التي تُحوّل الـ dictionary لحقول concrete مثل UsedListingsCount)
        var snapshot = new Dictionary<string, int>(sub.Used);
        var current = snapshot.GetValueOrDefault(quotaKey, 0);
        snapshot[quotaKey] = current + 1;
        sub.Used = snapshot;

        await _provider.UpdateSubscriptionAsync(sub, context.CancellationToken);

        // كتابة معرّف الاشتراك في الـ context للاستفادة منه في الـ Execute (للربط في كيانات التطبيق)
        context.Set("consumed_subscription_id", sub.Id);

        return new AnalyzerResult
        {
            Passed = true,
            Message = $"consumed_1_{quotaKey}",
            Data = new Dictionary<string, object>
            {
                ["subscription_id"] = sub.Id,
                ["quota_key"] = quotaKey,
                ["used_now"] = current + 1
            }
        };
    }
}
