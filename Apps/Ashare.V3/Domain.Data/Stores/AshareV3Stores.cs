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

    /// <summary>
    /// يُرجِع <c>Profile.UserId</c> (نَفس AspNetUsers.Id المُستَخدَم في
    /// asharedb V1/V2 — مِفتاح Favorites.UserId، ChatParticipants.UserId،
    /// Messages.SenderId، إلخ.). يَبحَث بِالـ <paramref name="subject"/>:
    /// <list type="bullet">
    ///   <item>أَوَّلاً عَلى <c>NationalId</c> (تَدَفُّق Nafath).</item>
    ///   <item>ثُمّ عَلى <c>PhoneNumber</c> (تَدَفُّق SMS).</item>
    /// </list>
    /// لَو وُجِد Profile قائِم بِلا UserId، نُعَيِّن واحِداً ثابِتاً.
    /// لَو لا profile، نُنشِئ واحِداً مَع subject في الحَقل المُناسِب.
    ///
    /// <para><b>لِماذا UserId لا Id</b>: في asharedb البَيانات الحَيَّة، كُلّ
    /// الجَداوِل المُتَفَرِّعَة (Favorite, ChatParticipant, Message…) تَحفَظ
    /// AspNetUsers.Id كَـ string. لَو رَدَدنا Profile.Id (Guid مَحَلِّي) لَن
    /// يَلتَقي مَع أَيّ بَيانات سابِقَة لِلمُستَخدِم نَفسه. هذا هو السَبَب
    /// المُباشِر لِسؤال المُستَخدِم: "لماذا لا يَتَعَرَّف عَلَيّ بِنَفس الهُوِيَّة؟"</para>
    /// </summary>
    public async Task<string> GetOrCreateUserIdAsync(string subject, CancellationToken ct)
    {
        subject = subject.Trim();
        // IgnoreQueryFilters: لو الـ Profile مُحَفَّمَة (IsDeleted=true) في
        // الإنتاج، نَتَجاوَز الفِلتَر ونُحييها بَدَل إنشاء مُكَرَّر. كُلّ
        // الجَداوِل المُتَفَرِّعَة في asharedb تُشير إلى الـ UserId الأَصلي.
        var existing = await _db.Profiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.NationalId == subject || p.Phone == subject, ct);

        if (existing is not null)
        {
            var dirty = false;
            if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.UpdatedAt = DateTime.UtcNow;
                dirty = true;
            }
            if (string.IsNullOrEmpty(existing.UserId))
            {
                existing.UserId = Guid.NewGuid().ToString();
                existing.UpdatedAt = DateTime.UtcNow;
                dirty = true;
            }
            if (dirty) await _db.SaveChangesAsync(ct);
            return existing.UserId!;
        }

        // مُستَخدِم جَديد. سَجِّل subject في الحَقل المُناسِب بِناءً عَلى الشَكل:
        // 10 أَرقام تَبدَأ بِـ 1/2 = National ID (السُّعودِيَّة)، غَير ذلك = هاتِف.
        var isNationalId = subject.Length == 10
                           && subject.All(char.IsDigit)
                           && (subject[0] == '1' || subject[0] == '2');
        var newUserId = Guid.NewGuid().ToString();
        var p = new ProfileEntity
        {
            Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            UserId = newUserId,
            NationalId = isNationalId ? subject : null,
            Phone = isNationalId ? null : subject,
            FullName = "عُضو جديد",
            City = "الرياض",
            // Type/IsActive سَمات ديناميكِيَّة الآن (في AttributesJson عَبر
            // AcAttrEditor)؛ المُستَخدِم الجَديد بِلا قِيَم ⇒ افتِراضي.
        };
        _db.Profiles.Add(p);
        await _db.SaveChangesAsync(ct);   // اِحفَظ فَوريّاً مِثل حالَة التَحديث
        return newUserId;
    }

    public async Task<string?> GetDisplayNameAsync(string userId, CancellationToken ct)
    {
        return await _db.Profiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            // BusinessName أَصبَحَ في AttributesJson (سَمَة ديناميكِيَّة)؛
            // عَرض الاسم يَعود لِـ FullName فَقَط. التَطبيقات الَّتي تَحتاج
            // اسم النَّشاط في chat headers تَستَخرِجه مِن AttributesJson
            // عَبر JSON_VALUE/json_extract (مُكلِف ⇒ نَتَجاوَز هُنا).
            .Select(p => p.FullName)
            .FirstOrDefaultAsync(ct);
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
        // userId = AspNetUsers.Id (Profile.UserId). الـ Profile.Id الداخِلي
        // مَخفي عَن طَبَقَة الـ Auth — كُلّ الجَداوِل المُتَفَرِّعَة تَحفَظ UserId.
        var p = await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        return p is null ? null : ToView(p);
    }

    public async Task<bool> UpdateNoSaveAsync(string userId, ProfileUpdate u, CancellationToken ct)
    {
        var p = await _db.Profiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (p is null) return false;
        if (!string.IsNullOrWhiteSpace(u.FullName)) p.FullName = u.FullName!;
        if (u.Phone     is not null) p.Phone     = u.Phone;
        if (u.Email     is not null) p.Email     = u.Email;
        if (u.City      is not null) p.City      = u.City;
        if (u.AvatarUrl is not null) p.AvatarUrl = u.AvatarUrl;
        if (u.AttributesJson is not null) p.AttributesJson = u.AttributesJson;
        p.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    private static IUserProfile ToView(ProfileEntity p) => new InMemoryUserProfile(
        Id: p.UserId ?? p.Id.ToString(),   // UserId هو الـ identity العامّ — Profile.Id داخِلي فَقَط
        FullName: p.FullName ?? "",
        Phone: p.Phone ?? "",
        PhoneVerified: p.PhoneVerified,
        Email: p.Email ?? "",
        EmailVerified: p.EmailVerified,
        City: p.City ?? "",
        AvatarUrl: p.AvatarUrl,
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
        // ownerId = AspNetUsers.Id (Profile.UserId). ProductListing.VendorId
        // مَع ذلك يُشير إلى Profile.Id (Guid داخِلي). نَحُلّ القَوس:
        // UserId → Profile.Id ثُمّ نُصَفّي القَوائِم.
        var vendorProfileId = await _db.Profiles.AsNoTracking()
            .Where(p => p.UserId == ownerId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (vendorProfileId is null) return Array.Empty<IListing>();
        var rows = await _db.ProductListings.AsNoTracking()
            .Where(l => l.VendorId == vendorProfileId.Value)
            .OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
        return rows.Cast<IListing>().ToList();
    }

    public async Task AddNoSaveAsync(IListing listing, CancellationToken ct)
    {
        // الكيت يُسَلِّم InMemoryListing — نُحَوِّله إلى ProductListingEntity.
        // OwnerId = AspNetUsers.Id (Profile.UserId). ProductListing.VendorId
        // يُريد Profile.Id (Guid داخِلي) ⇒ نَحُلّ القَوس.
        var ownerUserId = listing.OwnerId;
        var vendorProfileId = await _db.Profiles.AsNoTracking()
            .Where(p => p.UserId == ownerUserId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (vendorProfileId is null)
            throw new InvalidOperationException(
                $"Cannot create listing: no Profile found for UserId={ownerUserId}");

        var newId = Guid.TryParse(listing.Id, out var lid) ? lid : Guid.NewGuid();
        // الـ AttributesJson خام كَ string عَلى InMemoryListing — مُتَوَفِّر
        // فَقَط لَو الـ caller (kit) ضَخّ القِيمَة. تَجاهُل آمِن لَو null.
        var attrsJson = (listing as InMemoryListing)?.AttributesJson;

        // ImagesJson / AmenitiesJson = JSON arrays. الكيت يُمَرِّر القائِمَتَين
        // كَ IReadOnlyList<string> ⇒ نَطوي.
        var imagesJson = listing.Images is { Count: > 0 }
            ? System.Text.Json.JsonSerializer.Serialize(listing.Images)
            : null;
        var amenitiesJson = listing.Amenities is { Count: > 0 }
            ? System.Text.Json.JsonSerializer.Serialize(listing.Amenities)
            : null;

        _db.ProductListings.Add(new ProductListingEntity
        {
            Id              = newId,
            CreatedAt       = DateTime.UtcNow,
            VendorId        = vendorProfileId.Value,
            Title           = listing.Title,
            Description     = listing.Description,
            Price           = listing.Price,
            TimeUnit        = listing.TimeUnit,
            Condition       = listing.PropertyType,
            City            = listing.City,
            Address         = listing.District,
            Latitude        = listing.Lat,
            Longitude       = listing.Lng,
            BedroomCount    = listing.BedroomCount,
            BathroomCount   = listing.BathroomCount,
            AreaSqm         = listing.AreaSqm,
            Status          = listing.Status,
            ViewCount       = listing.ViewsCount,
            IsActive        = true,
            IsFeatured      = listing.IsVerified,
            FeaturedImage   = listing.ThumbnailUrl,
            ImagesJson      = imagesJson,
            AmenitiesJson   = amenitiesJson,
            AttributesJson  = attrsJson,
        });
    }

    public async Task<bool> UpdateNoSaveAsync(string id, ListingUpdate p, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var gid)) return false;
        var l = await _db.ProductListings.FirstOrDefaultAsync(x => x.Id == gid, ct);
        if (l is null) return false;

        // كُلّ حَقل null = "لا تَلمَسه" (PATCH semantics).
        if (p.Title         is not null) l.Title         = p.Title;
        if (p.Description   is not null) l.Description   = p.Description;
        if (p.Price.HasValue)            l.Price         = p.Price.Value;
        if (p.TimeUnit      is not null) l.TimeUnit      = p.TimeUnit;
        if (p.PropertyType  is not null) l.Condition     = p.PropertyType;
        if (p.City          is not null) l.City          = p.City;
        if (p.District      is not null) l.Address       = p.District;
        if (p.Lat.HasValue)              l.Latitude      = p.Lat.Value;
        if (p.Lng.HasValue)              l.Longitude     = p.Lng.Value;
        if (p.BedroomCount.HasValue)     l.BedroomCount  = p.BedroomCount.Value;
        if (p.BathroomCount.HasValue)    l.BathroomCount = p.BathroomCount.Value;
        if (p.AreaSqm.HasValue)          l.AreaSqm       = p.AreaSqm.Value;
        if (p.Amenities is not null)
            l.AmenitiesJson = p.Amenities.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(p.Amenities)
                : null;
        if (p.Images is not null)
            l.ImagesJson = p.Images.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(p.Images)
                : null;
        if (p.Thumbnail     is not null) l.FeaturedImage  = p.Thumbnail;
        if (p.AttributesJson is not null) l.AttributesJson = p.AttributesJson;

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

    /// <summary>
    /// F6 hook الَّذي يَستَدعيه <c>ChatController.Send</c>. لا
    /// <c>SaveChangesAsync</c> هُنا — الـ controller يَحفَظ ذرّيّاً بِـ
    /// <c>SaveAtEnd</c>. كان غِيابه يَجعَل الرَسائِل تَختَفي بِلا خَطَأ.
    /// </summary>
    public async Task AppendNoSaveAsync(IChatMessage message, CancellationToken ct)
    {
        if (!Guid.TryParse(message.ConversationId, out var cid)) return;
        // SenderPartyId يَأتي كَـ "User:GUID" — نَستَخرِج جُزء الـ id.
        var senderId = message.SenderPartyId.Contains(':')
            ? message.SenderPartyId[(message.SenderPartyId.IndexOf(':') + 1)..]
            : message.SenderPartyId;
        _db.Messages.Add(new MessageEntity
        {
            Id        = Guid.TryParse(message.Id, out var mid) ? mid : Guid.NewGuid(),
            CreatedAt = message.SentAt == default ? DateTime.UtcNow : message.SentAt,
            ChatId    = cid,
            SenderId  = senderId,
            Content   = message.Body,
            Type      = 0,
        });
        await Task.CompletedTask;
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

    public async Task<IReadOnlyList<IChatConversationView>> ListForUserAsync(string userId, CancellationToken ct)
    {
        // الواجِهَة (Chats.razor → ConvDto) تَتَوَقَّع شَكلاً غَنيّاً —
        // OwnerName/PartnerName/Avatars/LastMessage/LastAt/UnreadCount.
        // الكيت يَتَوَقَّع typed <see cref="IChatConversationView"/> ⇒
        // نَبني الـ POCO <see cref="ChatConversationView"/> مُباشَرَةً.
        var chatIds = await _db.ChatParticipants.AsNoTracking()
            .Where(p => p.UserId == userId).Select(p => p.ChatId).ToListAsync(ct);
        if (chatIds.Count == 0) return Array.Empty<IChatConversationView>();

        var chats = await _db.Chats.AsNoTracking()
            .Where(c => chatIds.Contains(c.Id))
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt).ToListAsync(ct);

        var parts = await _db.ChatParticipants.AsNoTracking()
            .Where(p => chatIds.Contains(p.ChatId)).ToListAsync(ct);
        var allUserIds = parts.Select(p => p.UserId).Distinct().ToList();

        var profiles = await _db.Profiles.AsNoTracking()
            .Where(p => allUserIds.Contains(p.UserId!))
            // BusinessName ديناميكي الآن — نَستَخدِم FullName فَقَط لِلعَرض.
            .Select(p => new { p.UserId, p.FullName, p.AvatarUrl })
            .ToListAsync(ct);
        var profByUser = profiles
            .Where(p => !string.IsNullOrEmpty(p.UserId))
            .GroupBy(p => p.UserId!).ToDictionary(g => g.Key, g => g.First());

        // آخِر رِسالَة لِكُلّ chat. SQLite ⇒ نَجلب مَجموعَة الـ ChatIds دَفعَة
        // واحِدَة ثُمّ نَحسب يَدَوِيّاً (تَجَنُّب window functions غَير المَدعومَة).
        var msgs = await _db.Messages.AsNoTracking()
            .Where(m => chatIds.Contains(m.ChatId))
            .Select(m => new { m.ChatId, m.Content, m.CreatedAt })
            .ToListAsync(ct);
        var lastByChat = msgs.GroupBy(m => m.ChatId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First());

        var partsByChat = parts.GroupBy(p => p.ChatId).ToDictionary(g => g.Key, g => g.ToList());

        var views = new List<IChatConversationView>(chats.Count);
        foreach (var c in chats)
        {
            var cps = partsByChat.GetValueOrDefault(c.Id) ?? new();
            var meSide = cps.FirstOrDefault(p => p.UserId == userId);
            var other  = cps.FirstOrDefault(p => p.UserId != userId);
            var meProf    = meSide   is null ? null : profByUser.GetValueOrDefault(meSide.UserId);
            var otherProf = other    is null ? null : profByUser.GetValueOrDefault(other.UserId);

            var last = lastByChat.GetValueOrDefault(c.Id);
            var participants = cps.Select(p => p.UserId)
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => $"User:{s}")
                .ToList();

            views.Add(new ChatConversationView(
                Id:                  c.Id.ToString(),
                ParticipantPartyIds: participants!,
                OwnerId:             meSide?.UserId ?? "",
                OwnerName:           meProf?.FullName ?? "—",
                OwnerAvatar:         meProf?.AvatarUrl,
                PartnerId:           other?.UserId ?? "",
                PartnerName:         otherProf?.FullName ?? "—",
                PartnerAvatar:       otherProf?.AvatarUrl,
                Subject:             c.Title ?? "",
                ListingId:           null,
                LastAt:              last?.CreatedAt ?? c.UpdatedAt ?? c.CreatedAt,
                UnreadCount:         0,
                LastMessage:         last?.Content,
                LastMessageAt:       last?.CreatedAt,
                HasMyUnread:         false));
        }
        return views;
    }

    // ConversationView القَديم حُذِفَ — كانَ يَعتَمِد عَلى السيريالايزر يَكتُب
    // runtime type. الآن نَبني <see cref="ChatConversationView"/> POCO المُعَرَّف
    // في <c>ACommerce.Chat.Operations</c> مُباشَرَةً.
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
