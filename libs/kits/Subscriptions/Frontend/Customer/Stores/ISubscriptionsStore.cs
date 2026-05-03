using ACommerce.Subscriptions.Operations.Abstractions;

namespace ACommerce.Kits.Subscriptions.Frontend.Customer.Stores;

/// <summary>store reactive لخطط + اشتراك المستخدِم الحاليّ.</summary>
public interface ISubscriptionsStore
{
    IReadOnlyList<IPlan> Plans { get; }
    ISubscription? Active { get; }
    bool IsLoading { get; }
    event Action? Changed;

    Task LoadPlansAsync(CancellationToken ct = default);
    Task LoadActiveAsync(CancellationToken ct = default);
    Task SubscribeAsync(string planId, CancellationToken ct = default);
}
