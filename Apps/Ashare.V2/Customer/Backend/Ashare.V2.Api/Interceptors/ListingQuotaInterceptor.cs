using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using Ashare.V2.Domain;

namespace Ashare.V2.Api.Interceptors;

/// <summary>
/// Cross-cutting quota check on listing creation.
///
/// يُطلَق على العمليّات التي تحمل tag <c>quota_listing</c>.
/// يحسب عدد الإعلانات الفعليّة للمستخدم ويقارنها بـ ActiveSubscription.ListingsLimit.
///
/// السبب في كونه Interceptor لا Analyzer:
///   منطق الحصة cross-cutting — لو أضفنا يوماً <c>listing.clone</c> أو
///   <c>listing.import</c>، تنطبق نفس السياسة بمجرّد وضع tag واحد.
/// </summary>
public sealed class ListingQuotaInterceptor : IOperationInterceptor
{
    public string Name => "ashare.quota.listings";
    public InterceptorPhase Phase => InterceptorPhase.Pre;

    public bool AppliesTo(Operation op) => op.HasTag("quota_listing");

    public Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? _ = null)
    {
        var caller = context.Operation.Parties.FirstOrDefault()?.Identity ?? string.Empty;
        var callerId = caller.Contains(':') ? caller.Split(':', 2)[1] : caller;

        var used = AshareV2Seed.Listings.Count(l =>
            l.OwnerId == callerId && l.Status == 1);
        var limit = AshareV2Seed.ActiveSubscription.ListingsLimit;

        if (used >= limit)
            return Task.FromResult(AnalyzerResult.Fail($"quota_exceeded: {used}/{limit}"));
        return Task.FromResult(AnalyzerResult.Pass($"{used}/{limit}"));
    }
}
