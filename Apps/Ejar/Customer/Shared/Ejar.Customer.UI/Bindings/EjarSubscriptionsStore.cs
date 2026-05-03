using ACommerce.Kits.Subscriptions.Frontend.Customer.Stores;
using ACommerce.Subscriptions.Operations.Abstractions;

namespace Ejar.Customer.UI.Bindings;

public sealed class EjarSubscriptionsStore : ISubscriptionsStore
{
    public IReadOnlyList<IPlan> Plans { get; private set; } = Array.Empty<IPlan>();
    public ISubscription? Active { get; private set; }
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public Task LoadPlansAsync(CancellationToken ct = default)             { Changed?.Invoke(); return Task.CompletedTask; }
    public Task LoadActiveAsync(CancellationToken ct = default)            { Changed?.Invoke(); return Task.CompletedTask; }
    public Task SubscribeAsync(string planId, CancellationToken ct = default){ Changed?.Invoke(); return Task.CompletedTask; }
}
