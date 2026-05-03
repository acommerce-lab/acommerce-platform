using ACommerce.Kits.Profiles.Operations;

namespace ACommerce.Kits.Profiles.Frontend.Customer.Stores;

/// <summary>store reactive لملفّ المستخدِم الحاليّ.</summary>
public interface IProfileStore
{
    IUserProfile? Current { get; }
    bool IsLoading { get; }
    event Action? Changed;

    Task LoadAsync(CancellationToken ct = default);
    Task UpdateAsync(IUserProfile next, CancellationToken ct = default);
}
