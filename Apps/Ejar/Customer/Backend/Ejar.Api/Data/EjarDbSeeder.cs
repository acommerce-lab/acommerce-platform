using Ejar.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ejar.Api.Data;

/// <summary>
/// عند بدء التشغيل: يضمن وجود الـ schema (EnsureCreated)، ولو كانت
/// قاعدة البيانات فارغة يبذر القيم الابتدائية من <see cref="EjarSeed"/>.
/// كذلك يُحدِّث الذاكرة الحيّة في <see cref="EjarSeed"/> لتعكس قاعدة البيانات
/// — حتى تواصل وحدات التحكّم القديمة العمل دون تعديل، ولكن البيانات تأتي
/// فعلياً من DB.
/// </summary>
public static class EjarDbSeeder
{
    public static async Task EnsureSchemaAndSeedAsync(IServiceProvider sp, ILogger logger)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EjarDbContext>();

        await db.Database.EnsureCreatedAsync();
        logger.LogInformation("Ejar.Db: schema ensured ({Provider})",
            db.Database.ProviderName);

        var alreadySeeded = await db.Listings.AsNoTracking().AnyAsync();
        if (!alreadySeeded)
        {
            logger.LogInformation("Ejar.Db: empty database — seeding from EjarSeed defaults...");
            await SeedFromInMemoryDefaultsAsync(db);
            logger.LogInformation("Ejar.Db: seeded {U} users, {L} listings, {C} conversations, {N} notifications, {P} plans",
                EjarSeed.Listings.Count, EjarSeed.Listings.Count, EjarSeed.Conversations.Count,
                EjarSeed.Notifications.Count, EjarSeed.Plans.Count);
        }

        await HydrateInMemoryFromDbAsync(db);
        logger.LogInformation("Ejar.Db: hydrated EjarSeed in-memory cache from DB " +
            "({Listings} listings, {Convs} conversations)",
            EjarSeed.Listings.Count, EjarSeed.Conversations.Count);
    }

    // ─── Seed: EjarSeed defaults → DB ──────────────────────────────────────
    private static async Task SeedFromInMemoryDefaultsAsync(EjarDbContext db)
    {
        // Users — العامّة وقت إنشاء seed تكون فقط U-1, U-2 (يضاف الباقي عبر OTP).
        foreach (var u in DefaultUsers())
            db.Users.Add(u);

        foreach (var l in EjarSeed.Listings.Select(ToEntity))
            db.Listings.Add(l);

        foreach (var c in EjarSeed.Conversations.Select(ToEntity))
            db.Conversations.Add(c);

        foreach (var n in EjarSeed.Notifications.Select(ToEntity))
            db.Notifications.Add(n);

        foreach (var p in EjarSeed.Plans.Select(ToEntity))
            db.Plans.Add(p);

        // Favorites: حفظ كأرقام مرتبطة بـ U-1 (المستخدم التجريبي الافتراضي).
        foreach (var lid in EjarSeed.FavoriteIds)
            db.Favorites.Add(new FavoriteEntity {
                Id = $"{EjarSeed.CurrentUserId}|{lid}",
                UserId = EjarSeed.CurrentUserId, ListingId = lid });

        await db.SaveChangesAsync();
    }

    private static IEnumerable<UserEntity> DefaultUsers()
    {
        // إعادة استخدام نفس قيم EjarSeed.GetUser("U-1"/"U-2") بإسقاط منفصل
        // لأن الـ private dictionary غير ظاهر — نقرأها عبر GetUser.
        foreach (var id in new[] { "U-1", "U-2" })
        {
            var u = EjarSeed.GetUser(id);
            if (u is null) continue;
            yield return new UserEntity {
                Id = u.Id, FullName = u.FullName, Phone = u.Phone,
                PhoneVerified = u.PhoneVerified, Email = u.Email,
                EmailVerified = u.EmailVerified, City = u.City,
                MemberSince = u.MemberSince
            };
        }
    }

    // ─── Hydrate: DB → EjarSeed in-memory cache ────────────────────────────
    private static async Task HydrateInMemoryFromDbAsync(EjarDbContext db)
    {
        var listings = await db.Listings.AsNoTracking().ToListAsync();
        EjarSeed.Listings.Clear();
        foreach (var e in listings) EjarSeed.Listings.Add(FromEntity(e));

        var convs = await db.Conversations
            .Include(c => c.Messages.OrderBy(m => m.SentAt))
            .AsNoTracking()
            .ToListAsync();
        EjarSeed.Conversations.Clear();
        foreach (var e in convs) EjarSeed.Conversations.Add(FromEntity(e));

        var notifs = await db.Notifications.AsNoTracking()
            .OrderByDescending(n => n.CreatedAt).ToListAsync();
        EjarSeed.Notifications.Clear();
        foreach (var e in notifs) EjarSeed.Notifications.Add(FromEntity(e));

        var favs = await db.Favorites.AsNoTracking()
            .Where(f => f.UserId == EjarSeed.CurrentUserId).Select(f => f.ListingId).ToListAsync();
        EjarSeed.FavoriteIds.Clear();
        foreach (var id in favs) EjarSeed.FavoriteIds.Add(id);
    }

    // ─── Mapping helpers ───────────────────────────────────────────────────
    private static ListingEntity ToEntity(EjarSeed.ListingSeed l) => new() {
        Id = l.Id, Title = l.Title, Description = l.Description,
        Price = l.Price, TimeUnit = l.TimeUnit, PropertyType = l.PropertyType,
        City = l.City, District = l.District, Lat = l.Lat, Lng = l.Lng,
        AmenitiesCsv = string.Join(',', l.Amenities ?? Array.Empty<string>()),
        OwnerId = l.OwnerId,
        BedroomCount = l.BedroomCount, BathroomCount = l.BathroomCount,
        AreaSqm = l.AreaSqm, IsVerified = l.IsVerified,
        ViewsCount = l.ViewsCount, Status = l.Status,
        ImagesCsv = string.Join(',', l.Images ?? Array.Empty<string>())
    };

    private static EjarSeed.ListingSeed FromEntity(ListingEntity e) => new(
        e.Id, e.Title, e.Description, e.Price, e.TimeUnit, e.PropertyType,
        e.City, e.District, e.Lat, e.Lng, SplitCsv(e.AmenitiesCsv) ?? Array.Empty<string>(),
        e.OwnerId, e.BedroomCount, e.BathroomCount, e.AreaSqm, e.IsVerified,
        e.ViewsCount, e.Status, SplitCsv(e.ImagesCsv));

    private static ConversationEntity ToEntity(EjarSeed.ConversationSeed c) => new() {
        Id = c.Id, PartnerName = c.PartnerName, PartnerId = c.PartnerId,
        ListingId = c.ListingId, Subject = c.Subject,
        LastAt = c.LastAt, UnreadCount = c.UnreadCount,
        Messages = c.Messages.Select(m => new MessageEntity {
            Id = m.Id, ConversationId = m.ConversationId, From = m.From,
            Text = m.Text, SentAt = m.SentAt
        }).ToList()
    };

    private static EjarSeed.ConversationSeed FromEntity(ConversationEntity e) => new(
        e.Id, e.PartnerName, e.PartnerId, e.ListingId, e.Subject, e.LastAt, e.UnreadCount,
        e.Messages.Select(m => new EjarSeed.MessageSeed(
            m.Id, m.ConversationId, m.From, m.Text, m.SentAt)).ToList());

    private static NotificationEntity ToEntity(EjarSeed.NotificationSeed n) => new() {
        Id = n.Id, Title = n.Title, Body = n.Body, CreatedAt = n.CreatedAt,
        IsRead = n.IsRead, RelatedId = n.RelatedId, Type = n.Type
    };
    private static EjarSeed.NotificationSeed FromEntity(NotificationEntity e) =>
        new(e.Id, e.Title, e.Body, e.CreatedAt, e.IsRead, e.RelatedId, e.Type);

    private static PlanEntity ToEntity(EjarSeed.PlanSeed p) => new() {
        Id = p.Id, Label = p.Name, Price = p.Price, CycleLabel = p.Unit,
        MaxActiveListings = p.ListingQuota, MaxFeaturedListings = p.FeaturedQuota,
        MaxImagesPerListing = p.ImagesPerListing, IsRecommended = p.Popular,
        Description = p.Description,
        FeaturesCsv = string.Join("|", p.Features ?? Array.Empty<string>())
    };

    private static IReadOnlyList<string>? SplitCsv(string s) =>
        string.IsNullOrEmpty(s) ? null
            : s.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
}
