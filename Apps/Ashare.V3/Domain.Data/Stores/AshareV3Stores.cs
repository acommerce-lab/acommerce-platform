using ACommerce.Chat.Operations;
using ACommerce.Favorites.Backend;
using ACommerce.Favorites.Operations.Entities;
using ACommerce.Kits.Auth.Operations;
using ACommerce.Kits.Chat.Backend;
using ACommerce.Kits.Listings.Backend;
using ACommerce.Kits.Listings.Domain;
using ACommerce.Kits.Listings.Operations;
using ACommerce.Kits.Profiles.Backend;
using ACommerce.Kits.Profiles.Operations;
using ACommerce.Kits.Versions.Backend;
using ACommerce.Kits.Versions.Operations;
using Ashare.V3.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Data.Stores;

// ═════════════════════════════════════════════════════════════════════════
// Ashare V3 stores — تَنفيذ kit interfaces فَوق جَداوِل asharedb مُباشَرَةً.
// لا migrations عَلى الجَداوِل القائِمَة، لا تَحويل بَيانات.
// ═════════════════════════════════════════════════════════════════════════


// ─── Auth ───────────────────────────────────────────────────────────────
public sealed class AshareV3AuthUserStore : IAuthUserStore
{
    private readonly AshareV3DbContext _db;
    public AshareV3AuthUserStore(AshareV3DbContext db) => _db = db;

    public async Task<string> GetOrCreateUserIdAsync(string phone, CancellationToken ct)
    {
        var existing = await _db.Profiles.FirstOrDefaultAsync(p => p.PhoneNumber == phone, ct);
        if (existing is not null) return existing.Id.ToString();

        var p = new ProfileEntity
        {
            Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            PhoneNumber = phone, FullName = "عُضو جديد",
            City = "صنعاء", IsActive = true, Type = 0
        };
        _db.Profiles.Add(p);
        return p.Id.ToString();
    }

    public async Task<string?> GetDisplayNameAsync(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var id)) return null;
        return await _db.Profiles.AsNoTracking()
            .Where(p => p.Id == id).Select(p => p.FullName).FirstOrDefaultAsync(ct);
    }
}


// ─── Versions ───────────────────────────────────────────────────────────
public sealed class AshareV3VersionStore : IVersionStore
{
    private readonly AshareV3DbContext _db;
    public AshareV3VersionStore(AshareV3DbContext db) => _db = db;

    public async Task<IReadOnlyList<AppVersion>> ListAsync(string? platform, CancellationToken ct)
    {
        var q = _db.AppVersions.AsNoTracking();
        if (!string.IsNullOrEmpty(platform)) q = q.Where(v => v.ApplicationCode == platform);
        var rows = await q.ToListAsync(ct);
        return rows.Select(ToContract).ToList();
    }

    public async Task<AppVersion?> GetAsync(string platform, string version, CancellationToken ct)
    {
        var row = await _db.AppVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.ApplicationCode == platform && v.VersionNumber == version, ct);
        return row is null ? null : ToContract(row);
    }

    public async Task<AppVersion?> GetLatestAsync(string platform, CancellationToken ct)
    {
        var row = await _db.AppVersions.AsNoTracking()
            .Where(v => v.ApplicationCode == platform && v.IsActive)
            .OrderByDescending(v => v.BuildNumber)
            .ThenByDescending(v => v.ReleaseDate).FirstOrDefaultAsync(ct);
        return row is null ? null : ToContract(row);
    }

    public async Task<AppVersion> UpsertAsync(AppVersion v, CancellationToken ct)
    {
        var row = await _db.AppVersions.FirstOrDefaultAsync(
            x => x.ApplicationCode == v.Platform && x.VersionNumber == v.Version, ct);
        if (row is null)
        {
            row = new AppVersionEntity
            {
                Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
                ApplicationCode = v.Platform, ApplicationNameAr = v.Platform,
                ApplicationNameEn = v.Platform, VersionNumber = v.Version,
                BuildNumber = 1, ReleaseDate = DateTime.UtcNow, IsActive = true
            };
            _db.AppVersions.Add(row);
        }
        row.Status = (int)v.Status;
        row.EndOfSupportDate = v.SunsetAt;
        row.ReleaseNotesAr = v.Notes;
        row.DownloadUrl = v.DownloadUrl;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToContract(row);
    }

    public async Task<bool> SetStatusAsync(string platform, string version,
        VersionStatus newStatus, DateTime? sunsetAt, CancellationToken ct)
    {
        var row = await _db.AppVersions.FirstOrDefaultAsync(
            v => v.ApplicationCode == platform && v.VersionNumber == version, ct);
        if (row is null) return false;
        row.Status = (int)newStatus;
        row.EndOfSupportDate = sunsetAt;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(string platform, string version, CancellationToken ct)
    {
        var row = await _db.AppVersions.FirstOrDefaultAsync(
            v => v.ApplicationCode == platform && v.VersionNumber == version, ct);
        if (row is null) return false;
        row.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static AppVersion ToContract(AppVersionEntity e) => new(
        Platform: e.ApplicationCode,
        Version: e.VersionNumber,
        Status: (VersionStatus)e.Status,
        SunsetAt: e.EndOfSupportDate,
        Notes: e.ReleaseNotesAr,
        DownloadUrl: e.DownloadUrl);
}


// ─── Profile ────────────────────────────────────────────────────────────
public sealed class AshareV3ProfileStore : IProfileStore
{
    private readonly AshareV3DbContext _db;
    public AshareV3ProfileStore(AshareV3DbContext db) => _db = db;

    public async Task<IUserProfile?> GetAsync(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var id)) return null;
        var p = await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : ToView(p);
    }

    public async Task<bool> UpdateNoSaveAsync(string userId, ProfileUpdate u, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var id)) return false;
        var p = await _db.Profiles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        if (!string.IsNullOrWhiteSpace(u.FullName)) p.FullName = u.FullName!;
        if (u.Phone     is not null) p.PhoneNumber = u.Phone;
        if (u.Email     is not null) p.Email       = u.Email;
        if (u.City      is not null) p.City        = u.City;
        if (u.AvatarUrl is not null) p.Avatar      = u.AvatarUrl;
        p.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    private static IUserProfile ToView(ProfileEntity p) => new InMemoryUserProfile(
        Id: p.Id.ToString(),
        FullName: p.FullName ?? "",
        Phone: p.PhoneNumber ?? "",
        PhoneVerified: p.IsVerified,
        Email: p.Email ?? "",
        EmailVerified: false,
        City: p.City ?? "",
        AvatarUrl: p.Avatar,
        MemberSince: p.CreatedAt);
}


// ─── Listings ───────────────────────────────────────────────────────────
public sealed class AshareV3ListingStore : IListingStore
{
    private readonly AshareV3DbContext _db;
    public AshareV3ListingStore(AshareV3DbContext db) => _db = db;

    public async Task<IReadOnlyList<IListing>> SearchAsync(ListingFilter f, CancellationToken ct)
    {
        var q = _db.ProductListings.AsNoTracking().Where(l => l.IsActive);
        if (!string.IsNullOrEmpty(f.City))         q = q.Where(l => l.City == f.City);
        if (!string.IsNullOrEmpty(f.PropertyType)) q = q.Where(l => l.Condition == f.PropertyType);
        if (!string.IsNullOrEmpty(f.Search))
            q = q.Where(l => l.Title.Contains(f.Search) || (l.Description != null && l.Description.Contains(f.Search)));
        if (f.PriceMin.HasValue) q = q.Where(l => l.Price >= f.PriceMin.Value);
        if (f.PriceMax.HasValue) q = q.Where(l => l.Price <= f.PriceMax.Value);
        var rows = await q.OrderByDescending(l => l.CreatedAt).Take(100).ToListAsync(ct);
        return rows.Cast<IListing>().ToList();
    }

    public async Task<int> CountAsync(ListingFilter f, CancellationToken ct)
    {
        var q = _db.ProductListings.AsNoTracking().Where(l => l.IsActive);
        if (!string.IsNullOrEmpty(f.City)) q = q.Where(l => l.City == f.City);
        return await q.CountAsync(ct);
    }

    public async Task<IListing?> GetAsync(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var gid)) return null;
        return await _db.ProductListings.AsNoTracking().FirstOrDefaultAsync(l => l.Id == gid, ct);
    }

    public async Task<IReadOnlyList<IListing>> ListByOwnerAsync(string ownerId, CancellationToken ct)
    {
        if (!Guid.TryParse(ownerId, out var oid)) return Array.Empty<IListing>();
        var rows = await _db.ProductListings.AsNoTracking()
            .Where(l => l.VendorId == oid).OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
        return rows.Cast<IListing>().ToList();
    }

    public Task AddNoSaveAsync(IListing listing, CancellationToken ct) => Task.CompletedTask;

    public async Task<bool> UpdateNoSaveAsync(string id, ListingUpdate p, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var gid)) return false;
        var l = await _db.ProductListings.FirstOrDefaultAsync(x => x.Id == gid, ct);
        if (l is null) return false;
        if (p.Title       is not null) l.Title       = p.Title;
        if (p.Description is not null) l.Description = p.Description;
        if (p.Price.HasValue)          l.Price       = p.Price.Value;
        if (p.City        is not null) l.City        = p.City;
        l.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public async Task<int?> ToggleStatusNoSaveAsync(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var gid)) return null;
        var l = await _db.ProductListings.FirstOrDefaultAsync(x => x.Id == gid, ct);
        if (l is null) return null;
        l.IsActive = !l.IsActive;
        l.UpdatedAt = DateTime.UtcNow;
        return l.IsActive ? 1 : 2;
    }

    public async Task<bool> DeleteNoSaveAsync(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var gid)) return false;
        var l = await _db.ProductListings.FirstOrDefaultAsync(x => x.Id == gid, ct);
        if (l is null) return false;
        l.IsDeleted = true;
        l.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public async Task<bool> IsOwnerAsync(string id, string ownerId, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var gid) || !Guid.TryParse(ownerId, out var oid)) return false;
        return await _db.ProductListings.AsNoTracking().AnyAsync(l => l.Id == gid && l.VendorId == oid, ct);
    }

    public async Task IncrementViewCountNoSaveAsync(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var gid)) return;
        var l = await _db.ProductListings.FirstOrDefaultAsync(x => x.Id == gid, ct);
        if (l is null) return;
        l.ViewCount++;
    }
}


// ─── Chat (n-participant model) ─────────────────────────────────────────
public sealed class AshareV3ChatStore : IChatStore
{
    private readonly AshareV3DbContext _db;
    public AshareV3ChatStore(AshareV3DbContext db) => _db = db;

    public async Task<bool> CanParticipateAsync(string conversationId, string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(conversationId, out var cid)) return false;
        return await _db.ChatParticipants.AsNoTracking()
            .AnyAsync(p => p.ChatId == cid && p.UserId == userId, ct);
    }

    public async Task<IChatMessage> AppendMessageAsync(string conversationId, string senderId, string body, CancellationToken ct)
    {
        var cid = Guid.Parse(conversationId);
        var m = new MessageEntity
        {
            Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            ChatId = cid, SenderId = senderId, Content = body, Type = 0
        };
        _db.Messages.Add(m);
        await _db.SaveChangesAsync(ct);
        return m;
    }

    public async Task<IReadOnlyList<IChatMessage>> GetMessagesAsync(string conversationId, CancellationToken ct)
    {
        if (!Guid.TryParse(conversationId, out var cid)) return Array.Empty<IChatMessage>();
        var rows = await _db.Messages.AsNoTracking()
            .Where(m => m.ChatId == cid).OrderBy(m => m.CreatedAt).ToListAsync(ct);
        return rows.Cast<IChatMessage>().ToList();
    }

    public async Task<IChatConversation?> GetConversationAsync(string conversationId, CancellationToken ct)
    {
        if (!Guid.TryParse(conversationId, out var cid)) return null;
        return await _db.Chats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == cid, ct);
    }

    public async Task<IReadOnlyList<IChatConversation>> ListForUserAsync(string userId, CancellationToken ct)
    {
        var chatIds = await _db.ChatParticipants.AsNoTracking()
            .Where(p => p.UserId == userId).Select(p => p.ChatId).ToListAsync(ct);
        var chats = await _db.Chats.Include(c => c.Participants)
            .Where(c => chatIds.Contains(c.Id))
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt).ToListAsync(ct);
        return chats.Cast<IChatConversation>().ToList();
    }
}

public sealed class AshareV3ChatPresenceProbe : IPresenceProbe
{
    public Task<bool> IsUserActiveInConversationAsync(string userId, string conversationId, CancellationToken ct = default)
        => Task.FromResult(false);
}


// ─── Favorites ──────────────────────────────────────────────────────────
public sealed class AshareV3FavoritesStore : IFavoritesStore
{
    private readonly AshareV3DbContext _db;
    public AshareV3FavoritesStore(AshareV3DbContext db) => _db = db;

    public async Task<IReadOnlyList<object>> ListMineAsync(string userId, CancellationToken ct)
    {
        var ids = await _db.Favorites.AsNoTracking()
            .Where(f => f.UserId == userId).Select(f => f.ListingId).ToListAsync(ct);
        if (ids.Count == 0) return Array.Empty<object>();
        var listings = await _db.ProductListings.AsNoTracking()
            .Where(l => ids.Contains(l.Id)).ToListAsync(ct);
        return listings.Select(l => (object)new
        {
            id = l.Id, title = l.Title, price = l.Price, city = l.City,
            firstImage = l.FeaturedImage, isVerified = l.IsFeatured,
            timeUnit = "monthly", propertyType = l.Condition ?? "",
            district = l.Address ?? "", bedroomCount = 0
        }).ToList();
    }

    public async Task<FavoriteToggleResult> ToggleNoSaveAsync(
        string userId, string entityType, string entityId, CancellationToken ct)
    {
        if (!Guid.TryParse(entityId, out var lid))
            return new FavoriteToggleResult(entityId, false);
        var existing = await _db.Favorites.FirstOrDefaultAsync(
            f => f.UserId == userId && f.ListingId == lid, ct);
        if (existing is null)
        {
            _db.Favorites.Add(new FavoriteEntity
            {
                Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
                UserId = userId, ListingId = lid
            });
            return new FavoriteToggleResult(entityId, true);
        }
        _db.Favorites.Remove(existing);
        return new FavoriteToggleResult(entityId, false);
    }
}
