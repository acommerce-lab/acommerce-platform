using ACommerce.Client.Operations;
using ACommerce.Kits.Profiles.Operations;

namespace ACommerce.Kits.Profiles.Frontend.Customer.Stores;

/// <summary>OAM-shaped (F61) — profile.get_mine / profile.update عَبر ITemplateEngine.</summary>
public sealed class DefaultProfileStore : IProfileStore
{
    private readonly ITemplateEngine _engine;
    public DefaultProfileStore(ITemplateEngine engine) => _engine = engine;

    public IUserProfile? Current { get; private set; }
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _engine.ExecuteAsync<InMemoryUserProfile>(ProfilesOps.GetMine(), ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                Current = env.Data;
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task UpdateAsync(IUserProfile next, CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<InMemoryUserProfile>(
            ProfilesOps.Update(), payload: next, ct: ct);
        if (env.Operation.Status == "Success" && env.Data is not null)
            Current = env.Data;
        Changed?.Invoke();
    }
}
