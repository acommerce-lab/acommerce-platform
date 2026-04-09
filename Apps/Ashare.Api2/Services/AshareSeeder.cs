using Ashare.Api2.Entities;
using ACommerce.SharedKernel.Abstractions.Repositories;

namespace Ashare.Api2.Services;

/// <summary>
/// بذر البيانات الأولية لعشير - يطابق الفئات الخمس في AshareSeedDataService.
/// </summary>
public class AshareSeeder
{
    public static class CategoryIds
    {
        public static readonly Guid Residential       = Guid.Parse("10000000-0000-0000-0001-000000000001");
        public static readonly Guid LookingForHousing = Guid.Parse("10000000-0000-0000-0001-000000000002");
        public static readonly Guid LookingForPartner = Guid.Parse("10000000-0000-0000-0001-000000000003");
        public static readonly Guid Administrative    = Guid.Parse("10000000-0000-0000-0001-000000000004");
        public static readonly Guid Commercial        = Guid.Parse("10000000-0000-0000-0001-000000000005");
    }

    public static class UserIds
    {
        public static readonly Guid OwnerAhmed   = Guid.Parse("00000000-0000-0000-0001-000000000001");
        public static readonly Guid CustomerSara = Guid.Parse("00000000-0000-0000-0001-000000000002");
        public static readonly Guid AdminUser    = Guid.Parse("00000000-0000-0000-0001-000000000003");
    }

    public static class ListingIds
    {
        public static readonly Guid Apartment1 = Guid.Parse("30000000-0000-0000-0001-000000000001");
        public static readonly Guid Villa1     = Guid.Parse("30000000-0000-0000-0001-000000000002");
        public static readonly Guid Office1    = Guid.Parse("30000000-0000-0000-0002-000000000001");
    }

    private readonly IRepositoryFactory _repoFactory;

    public AshareSeeder(IRepositoryFactory repoFactory) => _repoFactory = repoFactory;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedCategoriesAsync(ct);
        await SeedUsersAsync(ct);
        await SeedListingsAsync(ct);
        await SeedPlansAsync(ct);
        await SeedDefaultSubscriptionsAsync(ct);
    }

    private async Task SeedPlansAsync(CancellationToken ct)
    {
        var repo = _repoFactory.CreateRepository<Plan>();
        if (await repo.CountAsync(cancellationToken: ct) > 0) return;

        foreach (var plan in AsharePlansSeed.GetAll())
            await repo.AddAsync(plan, ct);
    }

    private async Task SeedDefaultSubscriptionsAsync(CancellationToken ct)
    {
        var subRepo = _repoFactory.CreateRepository<Subscription>();
        if (await subRepo.CountAsync(cancellationToken: ct) > 0) return;

        // اشتراك تجريبي للمالك أحمد على باقة المنشآت السنوية
        var now = DateTime.UtcNow;
        await subRepo.AddAsync(new Subscription
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            UserId = UserIds.OwnerAhmed,
            PlanId = AsharePlansSeed.PlanIds.BusinessAnnual,
            BillingCycle = "annual",
            StartDate = now,
            EndDate = now.AddYears(1),
            Status = SubscriptionStatus.Active,
            AmountPaid = 4800,
            Currency = "SAR"
        }, ct);
    }

    private async Task SeedCategoriesAsync(CancellationToken ct)
    {
        var repo = _repoFactory.CreateRepository<Category>();
        if (await repo.CountAsync(cancellationToken: ct) > 0) return;

        var categories = new List<Category>
        {
            new()
            {
                Id = CategoryIds.Residential,
                Slug = "residential",
                NameAr = "سكني",
                NameEn = "Residential",
                Description = "عرض سكني (شقة/فيلا/استوديو/غرفة)",
                Icon = "home",
                SortOrder = 1
            },
            new()
            {
                Id = CategoryIds.LookingForHousing,
                Slug = "looking-for-housing",
                NameAr = "طلب سكن",
                NameEn = "Looking for Housing",
                Description = "أبحث عن سكن",
                Icon = "search",
                SortOrder = 2
            },
            new()
            {
                Id = CategoryIds.LookingForPartner,
                Slug = "looking-for-partner",
                NameAr = "طلب شريك سكن",
                NameEn = "Looking for Roommate",
                Description = "أبحث عن شريك سكن",
                Icon = "users",
                SortOrder = 3
            },
            new()
            {
                Id = CategoryIds.Administrative,
                Slug = "administrative",
                NameAr = "مساحة إدارية",
                NameEn = "Administrative",
                Description = "مساحات إدارية ومكاتب",
                Icon = "briefcase",
                SortOrder = 4
            },
            new()
            {
                Id = CategoryIds.Commercial,
                Slug = "commercial",
                NameAr = "مساحة تجارية",
                NameEn = "Commercial",
                Description = "مساحات تجارية ومحلات",
                Icon = "store",
                SortOrder = 5
            }
        };

        foreach (var c in categories)
        {
            c.CreatedAt = DateTime.UtcNow;
            await repo.AddAsync(c, ct);
        }
    }

    private async Task SeedUsersAsync(CancellationToken ct)
    {
        var repo = _repoFactory.CreateRepository<User>();
        if (await repo.CountAsync(cancellationToken: ct) > 0) return;

        var now = DateTime.UtcNow;
        await repo.AddRangeAsync(new[]
        {
            new User
            {
                Id = UserIds.OwnerAhmed,
                CreatedAt = now,
                PhoneNumber = "+966500000001",
                Email = "ahmed@ashare.test",
                FullName = "أحمد المالك",
                NationalId = "1000000001",
                NafathVerified = true,
                IsActive = true,
                Role = "owner"
            },
            new User
            {
                Id = UserIds.CustomerSara,
                CreatedAt = now,
                PhoneNumber = "+966500000002",
                Email = "sara@ashare.test",
                FullName = "سارة العميلة",
                NationalId = "2000000002",
                NafathVerified = true,
                IsActive = true,
                Role = "customer"
            },
            new User
            {
                Id = UserIds.AdminUser,
                CreatedAt = now,
                PhoneNumber = "+966500000003",
                Email = "admin@ashare.test",
                FullName = "المسؤول",
                IsActive = true,
                Role = "admin"
            }
        }, ct);
    }

    private async Task SeedListingsAsync(CancellationToken ct)
    {
        var repo = _repoFactory.CreateRepository<Listing>();
        if (await repo.CountAsync(cancellationToken: ct) > 0) return;

        var now = DateTime.UtcNow;
        await repo.AddRangeAsync(new[]
        {
            new Listing
            {
                Id = ListingIds.Apartment1,
                CreatedAt = now,
                OwnerId = UserIds.OwnerAhmed,
                CategoryId = CategoryIds.Residential,
                Title = "شقة فاخرة في حي الياسمين",
                Description = "شقة 3 غرف نوم، صالة، مطبخ، 2 حمام. مفروشة بالكامل.",
                Price = 4500m,
                Duration = 1,
                TimeUnit = "month",
                City = "الرياض",
                District = "الياسمين",
                PropertyType = "apartment",
                UnitType = "apartment",
                Floor = 3,
                Area = 140,
                Rooms = 3,
                Bathrooms = 2,
                Furnished = true,
                Amenities = "wifi,ac,elevator,parking",
                LicenseNumber = "RG-1234567",
                Status = ListingStatus.Published,
                PublishedAt = now
            },
            new Listing
            {
                Id = ListingIds.Villa1,
                CreatedAt = now,
                OwnerId = UserIds.OwnerAhmed,
                CategoryId = CategoryIds.Residential,
                Title = "فيلا واسعة بمسبح",
                Description = "فيلا دورين، 5 غرف نوم، حديقة ومسبح خاص.",
                Price = 12000m,
                Duration = 1,
                TimeUnit = "month",
                City = "جدة",
                District = "الشاطئ",
                PropertyType = "villa",
                Area = 450,
                Rooms = 5,
                Bathrooms = 4,
                Furnished = false,
                LicenseNumber = "RG-7654321",
                Status = ListingStatus.Published,
                PublishedAt = now
            },
            new Listing
            {
                Id = ListingIds.Office1,
                CreatedAt = now,
                OwnerId = UserIds.OwnerAhmed,
                CategoryId = CategoryIds.Commercial,
                Title = "مكتب تجاري مجهز",
                Description = "مكتب 80م، مجهز بالأثاث، إنترنت، استقبال.",
                Price = 6000m,
                Duration = 1,
                TimeUnit = "month",
                City = "الرياض",
                District = "العليا",
                PropertyType = "office",
                Floor = 5,
                Area = 80,
                LicenseNumber = "CO-9876543",
                Status = ListingStatus.Published,
                PublishedAt = now
            }
        }, ct);
    }
}
