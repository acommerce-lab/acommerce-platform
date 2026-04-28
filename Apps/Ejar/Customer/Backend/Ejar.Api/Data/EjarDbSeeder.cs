using Ejar.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ejar.Api.Data;

/// <summary>
/// عند بدء التشغيل: يضمن وجود الـ schema (EnsureCreated)، ولو كانت
/// قاعدة البيانات فارغة يبذر القيم الابتدائية.
/// يعمل على SQLite في التطوير و MSSQL في الإنتاج.
/// </summary>
public static class EjarDbSeeder
{
    // ─── Fixed Guid seeds لضمان ثبات الهوية عبر عمليات إعادة التشغيل ─────
    public static readonly Guid User1Id = new("00000001-0000-0000-0000-000000000001");
    public static readonly Guid User2Id = new("00000001-0000-0000-0000-000000000002");

    public static async Task EnsureSchemaAndSeedAsync(IServiceProvider sp, ILogger logger)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EjarDbContext>();

        await db.Database.EnsureCreatedAsync();
        logger.LogInformation("Ejar.Db: schema ensured ({Provider})", db.Database.ProviderName);

        var alreadySeeded = await db.Listings.IgnoreQueryFilters().AnyAsync();
        if (!alreadySeeded)
        {
            logger.LogInformation("Ejar.Db: empty database — seeding...");
            await SeedAsync(db);
            logger.LogInformation("Ejar.Db: seeding complete");
        }
    }

    private static async Task SeedAsync(EjarDbContext db)
    {
        var now = DateTime.UtcNow;

        // ─── Users ──────────────────────────────────────────────────────
        var u1 = new UserEntity {
            Id = User1Id, FullName = "محمد أحمد", Phone = "777123456",
            PhoneVerified = true, Email = "mohammed@example.com", EmailVerified = true,
            City = "صنعاء", MemberSince = new DateTime(2024, 1, 15), CreatedAt = now
        };
        var u2 = new UserEntity {
            Id = User2Id, FullName = "علي حسين", Phone = "771987654",
            PhoneVerified = true, Email = "ali@example.com", EmailVerified = false,
            City = "عدن", MemberSince = new DateTime(2024, 3, 20), CreatedAt = now
        };
        db.Users.AddRange(u1, u2);

        // ─── Listings ───────────────────────────────────────────────────
        var listings = CreateSeedListings(now);
        db.Listings.AddRange(listings);

        // ─── Conversations + Messages ───────────────────────────────────
        var conv1Id = Guid.NewGuid();
        var conv1 = new ConversationEntity {
            Id = conv1Id, PartnerName = "علي حسين", PartnerId = User2Id,
            ListingId = listings[0].Id, Subject = listings[0].Title,
            LastAt = now.AddMinutes(-30), UnreadCount = 1, CreatedAt = now,
            Messages = new List<MessageEntity> {
                new() { Id = Guid.NewGuid(), ConversationId = conv1Id, From = "me",
                    Text = "السلام عليكم، هل الشقة متاحة؟", SentAt = now.AddHours(-2), CreatedAt = now },
                new() { Id = Guid.NewGuid(), ConversationId = conv1Id, From = "other",
                    Text = "وعليكم السلام، نعم متاحة تفضل", SentAt = now.AddMinutes(-30), CreatedAt = now }
            }
        };
        db.Conversations.Add(conv1);

        // ─── Notifications ──────────────────────────────────────────────
        db.Notifications.AddRange(
            new NotificationEntity { Id = Guid.NewGuid(), Title = "إعلان جديد في صنعاء",
                Body = "تم نشر إعلان شقة مفروشة في حدة", Type = "listing", CreatedAt = now.AddHours(-1) },
            new NotificationEntity { Id = Guid.NewGuid(), Title = "رسالة جديدة",
                Body = "لديك رسالة جديدة من علي حسين", Type = "message", CreatedAt = now.AddMinutes(-30) },
            new NotificationEntity { Id = Guid.NewGuid(), Title = "تم تجديد اشتراكك",
                Body = "تم تجديد اشتراكك بنجاح — باقة المحترف", Type = "subscription", CreatedAt = now.AddDays(-1) }
        );

        // ─── Plans ──────────────────────────────────────────────────────
        var planFreeId = Guid.NewGuid();
        var planProId  = Guid.NewGuid();
        var planBizId  = Guid.NewGuid();
        db.Plans.AddRange(
            new PlanEntity { Id = planFreeId, Label = "مجاني", Price = 0, CycleLabel = "شهرياً",
                MaxActiveListings = 2, MaxFeaturedListings = 0, MaxImagesPerListing = 3,
                Description = "للمبتدئين", FeaturesCsv = "إعلانان|3 صور", CreatedAt = now },
            new PlanEntity { Id = planProId, Label = "المحترف", Price = 99, CycleLabel = "شهرياً",
                MaxActiveListings = 20, MaxFeaturedListings = 5, MaxImagesPerListing = 10,
                IsRecommended = true, Description = "للمكاتب العقارية",
                FeaturesCsv = "20 إعلان|5 مميزة|10 صور|دعم فني", CreatedAt = now },
            new PlanEntity { Id = planBizId, Label = "الأعمال", Price = 299, CycleLabel = "شهرياً",
                MaxActiveListings = 100, MaxFeaturedListings = 20, MaxImagesPerListing = 20,
                Description = "للشركات الكبيرة",
                FeaturesCsv = "100 إعلان|20 مميزة|20 صور|دعم مخصص|تقارير", CreatedAt = now }
        );

        // ─── Subscription ───────────────────────────────────────────────
        db.Subscriptions.Add(new SubscriptionEntity {
            Id = Guid.NewGuid(), UserId = User1Id, PlanId = planProId, PlanName = "المحترف",
            Status = "active", StartDate = now.AddDays(-15), EndDate = now.AddDays(15),
            ListingsLimit = 20, FeaturedLimit = 5, ImagesPerListing = 10, CreatedAt = now
        });

        // ─── Invoices ───────────────────────────────────────────────────
        db.Invoices.Add(new InvoiceEntity {
            Id = Guid.NewGuid(), UserId = User1Id, PlanId = planProId,
            Amount = 99, Date = now.AddDays(-15), Status = "paid", CreatedAt = now
        });

        // ─── Favorites ──────────────────────────────────────────────────
        if (listings.Count >= 2)
        {
            db.Favorites.AddRange(
                new FavoriteEntity { Id = Guid.NewGuid(), UserId = User1Id, ListingId = listings[0].Id, CreatedAt = now },
                new FavoriteEntity { Id = Guid.NewGuid(), UserId = User1Id, ListingId = listings[1].Id, CreatedAt = now }
            );
        }

        // ─── Complaints ─────────────────────────────────────────────────
        var comp1Id = Guid.NewGuid();
        db.Complaints.Add(new ComplaintEntity {
            Id = comp1Id, Subject = "إعلان مخالف", Body = "الإعلان يحتوي على معلومات مضللة",
            Status = "open", Priority = "عادي", RelatedEntity = listings[0].Id.ToString(),
            UserId = User1Id, CreatedAt = now.AddDays(-2),
            Replies = new List<ComplaintReplyEntity> {
                new() { Id = Guid.NewGuid(), ComplaintId = comp1Id, From = "user",
                    Message = "الإعلان يحتوي على معلومات مضللة", CreatedAt = now.AddDays(-2) },
                new() { Id = Guid.NewGuid(), ComplaintId = comp1Id, From = "support",
                    Message = "شكراً لتبليغك، سنراجع الإعلان خلال 24 ساعة", CreatedAt = now.AddDays(-1) }
            }
        });

        await db.SaveChangesAsync();
    }

    private static List<ListingEntity> CreateSeedListings(DateTime now)
    {
        var listings = new List<ListingEntity>();
        var seedData = new[] {
            ("شقة مفروشة في حدة", "شقة مفروشة بالكامل، 3 غرف نوم، قريبة من الخدمات", 1500m, "monthly", "apartment", "صنعاء", "حدة", 15.35, 44.21, "ac,wifi,kitchen,parking", 3, 2, 120, true),
            ("فيلا فاخرة في خلدا", "فيلا واسعة مع حديقة ومسبح خاص", 5000m, "monthly", "villa", "صنعاء", "خلدا", 15.37, 44.19, "ac,wifi,kitchen,parking,pool,gym", 5, 4, 350, true),
            ("استوديو في المنصورة", "استوديو مفروش مناسب للأفراد", 500m, "monthly", "apartment", "عدن", "المنصورة", 12.79, 45.03, "ac,wifi", 1, 1, 40, false),
            ("مكتب تجاري في شارع الزبيري", "مكتب تجاري في موقع حيوي", 2000m, "monthly", "office", "صنعاء", "شارع الزبيري", 15.36, 44.20, "ac,wifi,parking", 0, 1, 80, false),
            ("شقة يومية في التواهي", "شقة يومية مطلة على البحر", 100m, "daily", "apartment", "عدن", "التواهي", 12.78, 45.02, "ac,wifi,kitchen", 2, 1, 90, true),
            ("غرفة بالساعة في المعلا", "غرفة اجتماعات مجهزة", 50m, "hourly", "office", "عدن", "المعلا", 12.80, 45.01, "ac,wifi", 0, 1, 30, false),
        };

        foreach (var (title, desc, price, tu, pt, city, dist, lat, lng, amenities, bed, bath, area, verified) in seedData)
        {
            listings.Add(new ListingEntity {
                Id = Guid.NewGuid(), Title = title, Description = desc,
                Price = price, TimeUnit = tu, PropertyType = pt,
                City = city, District = dist, Lat = lat, Lng = lng,
                AmenitiesCsv = amenities, OwnerId = User1Id,
                BedroomCount = bed, BathroomCount = bath, AreaSqm = area,
                IsVerified = verified, ViewsCount = Random.Shared.Next(10, 500),
                Status = 1, CreatedAt = now
            });
        }
        return listings;
    }
}
