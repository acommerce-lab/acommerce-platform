using ACommerce.Kits.Profiles.Operations;

namespace ACommerce.Kits.Profiles.Frontend.Customer.Stores;

/// <summary>
/// تَنفيذ افتراضيّ لـ <see cref="IProfileStore"/> يَدلّع لـ
/// <see cref="IProfileApiClient"/>.
/// </summary>
public sealed class DefaultProfileStore : IProfileStore
{
    private readonly IProfileApiClient _api;
    public DefaultProfileStore(IProfileApiClient api) => _api = api;

    public IUserProfile? Current { get; private set; }
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var p = await _api.GetMineAsync(ct);
            if (p is not null) Current = p;
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task UpdateAsync(IUserProfile next, CancellationToken ct = default)
    {
        var p = await _api.UpdateAsync(next, ct);
        if (p is not null) Current = p;
        Changed?.Invoke();
    }
}
