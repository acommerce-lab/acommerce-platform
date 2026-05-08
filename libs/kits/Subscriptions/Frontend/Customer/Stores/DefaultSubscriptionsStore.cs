using ACommerce.Subscriptions.Operations.Abstractions;

namespace ACommerce.Kits.Subscriptions.Frontend.Customer.Stores;

/// <summary>
/// تَنفيذ افتراضيّ لـ <see cref="ISubscriptionsStore"/> يَدلّع لـ
/// <see cref="ISubscriptionsApiClient"/>.
/// </summary>
public sealed class DefaultSubscriptionsStore : ISubscriptionsStore
{
    private readonly ISubscriptionsApiClient _api;
    private List<IPlan> _plans = new();
    public DefaultSubscriptionsStore(ISubscriptionsApiClient api) => _api = api;

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
