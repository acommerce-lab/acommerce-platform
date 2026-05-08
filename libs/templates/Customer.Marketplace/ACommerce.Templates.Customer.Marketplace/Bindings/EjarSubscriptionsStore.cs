using ACommerce.Kits.Subscriptions.Frontend.Customer.Stores;
using ACommerce.Subscriptions.Operations.Abstractions;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="ISubscriptionsStore"/> لإيجار. يَدلّع للـ
/// <see cref="ISubscriptionsApiClient"/>.
/// </summary>
public sealed class EjarSubscriptionsStore : ISubscriptionsStore
{
    private readonly ISubscriptionsApiClient _api;
    private List<IPlan> _plans = new();

    public EjarSubscriptionsStore(ISubscriptionsApiClient api) => _api = api;

    public IReadOnlyList<IPlan> Plans => _plans;
    public ISubscription? Active { get; private set; }
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadPlansAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try   { _plans = (await _api.ListPlansAsync(ct)).ToList(); }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task LoadActiveAsync(CancellationToken ct = default)
    {
        Active = await _api.GetActiveAsync(ct);
        Changed?.Invoke();
    }

    public async Task SubscribeAsync(string planId, CancellationToken ct = default)
    {
        var sub = await _api.ActivateAsync(planId, ct);
        if (sub is not null) Active = sub;
        Changed?.Invoke();
    }
}
