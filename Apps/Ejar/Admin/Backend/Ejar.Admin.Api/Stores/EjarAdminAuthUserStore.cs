using ACommerce.Kits.Auth.Backend;
using Ejar.Domain;

namespace Ejar.Admin.Api.Stores;

public sealed class EjarAdminAuthUserStore : IAuthUserStore
{
    public Task<string> GetOrCreateUserIdAsync(string phone, CancellationToken ct)
        => Task.FromResult(EjarSeed.GetOrCreateUserId(phone));

    public Task<string?> GetDisplayNameAsync(string userId, CancellationToken ct)
        => Task.FromResult(EjarSeed.GetUser(userId)?.FullName);
}
