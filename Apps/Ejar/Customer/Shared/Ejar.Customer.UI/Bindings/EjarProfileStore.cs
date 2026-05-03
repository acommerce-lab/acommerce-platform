using ACommerce.Kits.Profiles.Frontend.Customer.Stores;
using ACommerce.Kits.Profiles.Operations;

namespace Ejar.Customer.UI.Bindings;

public sealed class EjarProfileStore : IProfileStore
{
    public IUserProfile? Current { get; private set; }
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public Task LoadAsync(CancellationToken ct = default)                     { Changed?.Invoke(); return Task.CompletedTask; }
    public Task UpdateAsync(IUserProfile next, CancellationToken ct = default){ Current = next; Changed?.Invoke(); return Task.CompletedTask; }
}
