using ACommerce.Kits.Profiles.Frontend.Customer.Stores;
using ACommerce.Kits.Profiles.Operations;

namespace Ejar.Customer.UI.V2.Bindings;

public sealed class EjarV2ProfileStore : IProfileStore
{
    private readonly IProfileApiClient _api;
    public EjarV2ProfileStore(IProfileApiClient api) => _api = api;

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
