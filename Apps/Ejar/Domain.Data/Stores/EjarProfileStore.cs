using ACommerce.Kits.Profiles.Backend;
using ACommerce.Kits.Profiles.Operations;
using Ejar.Api.Data;
using Ejar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Stores;

/// <summary>
/// مخزن profile إيجار — يربط Profiles kit بكيان <see cref="UserEntity"/>.
/// </summary>
public sealed class EjarProfileStore : IProfileStore
{
    private readonly EjarDbContext _db;
    public EjarProfileStore(EjarDbContext db) => _db = db;

    public async Task<IUserProfile?> GetAsync(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var uid)) return null;
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
        return u is null ? null : ToView(u);
    }

    public async Task<bool> UpdateNoSaveAsync(string userId, ProfileUpdate p, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var uid)) return false;
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == uid, ct);
        if (u is null) return false;

        if (!string.IsNullOrWhiteSpace(p.FullName))  u.FullName  = p.FullName!;
        if (p.Phone     is not null)                 u.Phone     = p.Phone;
        if (p.Email     is not null)                 u.Email     = p.Email;
        if (p.City      is not null)                 u.City      = p.City;
        if (p.AvatarUrl is not null)                 u.AvatarUrl = p.AvatarUrl;
        u.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    private static IUserProfile ToView(UserEntity u) => new InMemoryUserProfile(
        Id:            u.Id.ToString(),
        FullName:      u.FullName,
        Phone:         u.Phone,
        PhoneVerified: u.PhoneVerified,
        Email:         u.Email,
        EmailVerified: u.EmailVerified,
        City:          u.City,
        AvatarUrl:     u.AvatarUrl,
        MemberSince:   u.MemberSince);
}
