using ACommerce.Kits.Profiles.Operations;

namespace ACommerce.Kits.Profiles.Frontend.Customer.Stores;

/// <summary>
/// عميل HTTP خاصّ بـ Profiles kit. يَعرف شكل ردّ <c>ProfilesController</c>
/// (<c>GET /me/profile</c> + <c>PUT /me/profile</c>) ويُقَشِّر الـ envelope.
/// </summary>
public interface IProfileApiClient
{
    Task<IUserProfile?> GetMineAsync(CancellationToken ct = default);
    Task<IUserProfile?> UpdateAsync(IUserProfile next, CancellationToken ct = default);
}
