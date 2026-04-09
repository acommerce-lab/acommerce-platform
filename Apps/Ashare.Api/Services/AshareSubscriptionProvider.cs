using ACommerce.SharedKernel.Abstractions.Repositories;
using ACommerce.Subscriptions.Operations.Abstractions;
using Ashare.Api.Entities;
using LibIPlan = ACommerce.Subscriptions.Operations.Abstractions.IPlan;
using LibISubscription = ACommerce.Subscriptions.Operations.Abstractions.ISubscription;

namespace Ashare.Api.Services;

/// <summary>
/// مزوّد الاشتراكات لعشير - يجسر بين كيانات التطبيق المادية (Plan, Subscription)
/// والتجريدات العامة (IPlan, ISubscription) المُستخدَمة في QuotaInterceptor.
///
/// المعترضات في مكتبة Subscriptions.Operations لا تعرف عن Ashare.Plan/Ashare.Subscription،
/// تتعامل مع الواجهات فقط - وهذا الـ provider هو الجسر الوحيد.
/// </summary>
public class AshareSubscriptionProvider : ISubscriptionProvider
{
    private readonly IBaseAsyncRepository<Plan> _planRepo;
    private readonly IBaseAsyncRepository<Subscription> _subRepo;

    public AshareSubscriptionProvider(IRepositoryFactory factory)
    {
        _planRepo = factory.CreateRepository<Plan>();
        _subRepo = factory.CreateRepository<Subscription>();
    }

    public async Task<IReadOnlyList<LibISubscription>> GetActiveSubscriptionsAsync(
        Guid userId, CancellationToken ct = default)
    {
        var subs = await _subRepo.GetAllWithPredicateAsync(s =>
            s.UserId == userId &&
            (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial));

        return subs
            .OrderBy(s => s.StartDate)
            .Cast<LibISubscription>()
            .ToList();
    }

    public async Task<LibIPlan?> GetPlanAsync(Guid planId, CancellationToken ct = default)
    {
        return await _planRepo.GetByIdAsync(planId, ct);
    }

    public async Task UpdateSubscriptionAsync(LibISubscription sub, CancellationToken ct = default)
    {
        if (sub is Subscription concrete)
            await _subRepo.UpdateAsync(concrete, ct);
    }
}
