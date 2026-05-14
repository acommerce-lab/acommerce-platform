using ACommerce.Favorites.Operations.Entities;
using ACommerce.Kits.Discovery.Domain;
using ACommerce.Kits.Support.Domain;
using Ejar.Api.Data;
using Ejar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Data;

public static class DbInitializer
{
    public static void Seed(EjarDbContext db)
    {
        if (db.Users.Any()) return;

        // 1. Seed Users
        var userMap = new Dictionary<string, Guid>();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        db.Users.Add(new UserEntity {
            Id = user1Id, FullName = "أمل عبدالله المؤيد", Phone = "+967771234567", PhoneVerified = true,
            Email = "amal@example.ye", EmailVerified = true, City = "صنعاء", MemberSince = new DateTime(2024, 3, 12),
            CreatedAt = new DateTime(2024, 3, 12)
        });
        db.Users.Add(new UserEntity {
            Id = user2Id, FullName = "فهد محمد الجمالي", Phone = "+967773456789", PhoneVerified = true,
            Email = "fahd@example.ye", EmailVerified = false, City = "عدن", MemberSince = new DateTime(2025, 1, 22),
            CreatedAt = new DateTime(2025, 1, 22)
        });

        userMap["U-1"] = user1Id;
        userMap["U-2"] = user2Id;

        // 2. Seed Categories
        foreach (var c in EjarSeed.Categories)
        {
            db.DiscoveryCategories.Add(new DiscoveryCategory {
                Slug = c.Id, Label = c.Label, Icon = c.Emoji, Kind = c.Kind, CreatedAt = DateTime.UtcNow
            });
        }

        // 3. Seed Regions — إب أوّلاً (السوق الافتراضيّ للإطلاق التجريبيّ).
        foreach (var city in new[] { "إب", "صنعاء", "عدن", "تعز", "الحديدة", "المكلا" })
        {
            db.DiscoveryRegions.Add(new DiscoveryRegion { Name = city, Level = 1, CreatedAt = DateTime.UtcNow });
        }

        // 4. Seed Amenities
        foreach (var a in EjarSeed.Amenities)
        {
            db.DiscoveryAmenities.Add(new DiscoveryAmenity { Slug = a.Id, Label = a.Label, CreatedAt = DateTime.UtcNow });
        }

        // 5. Seed Plans (subscription catalog) — تظهر في صفحة /plans
        var plans = new[]
        {
            new PlanEntity { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
                Label = "مجاني",   Price = 0,     CycleLabel = "شهري",
                MaxActiveListings = 1,  MaxFeaturedListings = 0, MaxImagesPerListing = 3,
                IsRecommended = false, Description = "للمعاينة. إعلان واحد، صور أساسيّة.",
                FeaturesCsv = "إعلان واحد,٣ صور,بحث أساسي" },
            new PlanEntity { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
                Label = "أساسي",   Price = 5000,  CycleLabel = "شهري",
                MaxActiveListings = 5,  MaxFeaturedListings = 1, MaxImagesPerListing = 8,
                IsRecommended = true,  Description = "لأصحاب الإعلانات الفرديّة.",
                FeaturesCsv = "٥ إعلانات,إعلان مميّز واحد,٨ صور لكل إعلان,دعم سريع" },
            new PlanEntity { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
                Label = "احترافي", Price = 15000, CycleLabel = "شهري",
                MaxActiveListings = 20, MaxFeaturedListings = 5, MaxImagesPerListing = 15,
                IsRecommended = false, Description = "للوكالات والمكاتب العقاريّة.",
                FeaturesCsv = "٢٠ إعلان,٥ إعلانات مميّزة,١٥ صورة,أولويّة في النتائج,تحليلات" },
        };
        foreach (var p in plans) db.Plans.Add(p);

        // 6. Seed Listings
        foreach (var l in EjarSeed.Listings)
        {
            var ownerId = userMap.TryGetValue(l.OwnerId, out var o) ? o : user2Id;
            db.Listings.Add(new ListingEntity
            {
                Title = l.Title, Description = l.Description, Price = l.Price, TimeUnit = l.TimeUnit,
                PropertyType = l.PropertyType, City = l.City, District = l.District,
                Lat = l.Lat, Lng = l.Lng, OwnerId = ownerId,
                BedroomCount = l.BedroomCount, BathroomCount = l.BathroomCount, AreaSqm = l.AreaSqm,
                IsVerified = l.IsVerified, ViewsCount = l.ViewsCount, Status = l.Status,
                ImagesCsv = l.Images != null ? string.Join(",", l.Images) : "",
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30))
            });
        }

        db.SaveChanges();
        SeedAppVersionsIfMissing(db);
    }

    /// <summary>
    /// يُحَدّث صفوف Favorites القديمة التي EntityType فيها = "ListingEntity"
    /// (من CatalogController قبل Q1) إلى "Listing" (الصيغة الحاليّة في
    /// FavoritesController.ToggleListing). idempotent — يَنجح حتى لو لا صفوف
    /// قديمة. هذا يَحلّ "المفضّلات لا تَظهر بَعد deploy" للمُستخدِمين الذين
    /// لديهم بيانات سابقة.
    /// </summary>
    public static void NormalizeFavoriteEntityType(EjarDbContext db)
    {
        try
        {
            var n = db.Database.ExecuteSqlRaw(
                "UPDATE Favorites SET EntityType = 'Listing' WHERE EntityType = 'ListingEntity'");
            // n يُسَجَّل في logs لو أُريد، لكن idempotent ⇒ لا داعي للأمان.
        }
        catch
        {
            // فَشِل DDL/Update — DB قد لا يَدعم هذا الـ provider. لا نُعَطِّل
            // الإقلاع. القراءة الدِفاعيّة في EjarFavoritesStore تُعَوِّض.
        }
    }

    /// <summary>
    /// بذور Discovery idempotent (Categories + Regions + Amenities). تَعمل
    /// عند كلّ إقلاع لتَغطية حالة DB القديم الذي فيه Users لكنّ جداول
    /// Discovery أُضيفت لاحقاً (Seed الرئيسيّ يَتجاوزها لو Users.Any).
    /// </summary>
    public static void SeedDiscoveryIfMissing(EjarDbContext db)
    {
        var addedAny = false;

        // per-slug check يَسمَح بِإضافَة فِئات جَديدَة (events/vehicles/outdoor)
        // عَلى قاعِدَة بَيانات قائِمَة بِلا wipe.
        var existingSlugs = db.DiscoveryCategories.IgnoreQueryFilters()
            .Select(c => c.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var c in EjarSeed.Categories)
        {
            if (existingSlugs.Contains(c.Id)) continue;
            db.DiscoveryCategories.Add(new ACommerce.Kits.Discovery.Domain.DiscoveryCategory {
                Slug = c.Id, Label = c.Label, Icon = c.Emoji, Kind = c.Kind,
                CreatedAt = DateTime.UtcNow,
            });
            addedAny = true;
        }

        if (!db.DiscoveryRegions.Any())
        {
            foreach (var city in new[] { "إب", "صنعاء", "عدن", "تعز", "الحديدة", "المكلا" })
                db.DiscoveryRegions.Add(new ACommerce.Kits.Discovery.Domain.DiscoveryRegion {
                    Name = city, Level = 1, CreatedAt = DateTime.UtcNow,
                });
            addedAny = true;
        }

        if (!db.DiscoveryAmenities.Any())
        {
            foreach (var a in EjarSeed.Amenities)
                db.DiscoveryAmenities.Add(new ACommerce.Kits.Discovery.Domain.DiscoveryAmenity {
                    Slug = a.Id, Label = a.Label, CreatedAt = DateTime.UtcNow,
                });
            addedAny = true;
        }

        if (addedAny) db.SaveChanges();
    }

    /// <summary>
    /// بذرة شَجَرَة Taxonomy لِفِئات الإعلانات (root="listing_categories").
    /// يَبني هَيكَل مُسَتَوَيَين: kinds (residential/commercial/leisure/events/
    /// vehicles/camps) عَلى المُستَوى الأَوَّل، ثُمّ كُلّ category مَن
    /// <see cref="EjarSeed.Categories"/> تَحت kindها.
    ///
    /// <para><b>Idempotent + reconciling</b>: يَفحَص كُلّ عُقدَة بِـ
    /// <c>(RootCode, Code)</c> ⇒ يُضيف ما هو غَير مَوجود؛ ويَنقُل الـ leaves
    /// لِأَب جَديد لَو تَغَيَّر <c>Kind</c> في الـ seed (مَثَل: نَقل
    /// <c>camp</c> مِن <c>outdoor</c> إلى <c>camps</c>)؛ ويُعَطِّل أَيّ kind
    /// لَم يَعُد مَوجوداً في الـ seed (مَثَل <c>outdoor</c> القَديم بَعد
    /// نَقل ابنه الوَحيد إلى <c>camps</c>).</para>
    /// </summary>
    public static void SeedTaxonomyIfMissing(EjarDbContext db)
    {
        const string root = "listing_categories";

        // كُلّ المَوجود حاليّاً ضِمن هذا الـ root — مَفهرَس بِالـ Code لِفَحص
        // سَريع per-node. نَجلِب الـ entity كامِلاً (لا anonymous) لِأَنّنا
        // نَحتاج تَعديل ParentId/IsActive في خُطوَة الـ reconcile.
        var existingNodes = db.TaxonomyNodes.IgnoreQueryFilters()
            .Where(t => t.RootCode == root)
            .ToList();
        var existingByCode = existingNodes.ToDictionary(
            n => n.Code, n => n, StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;

        // ① عُقَد المُستَوى الأَوَّل (kinds). الـ tuple يَحوي SortOrder صَريح.
        // <c>outdoor</c> أُسقِطَ: المُخَيَّمات لَها kind مُستَقِلّ <c>camps</c>.
        var kinds = new (string Code, string NameAr, string Icon, int Sort)[]
        {
            ("residential", "سكني",      "home",       1),
            ("commercial",  "تجاري",     "briefcase",  2),
            ("leisure",     "ترفيهي",    "sun",        3),
            ("events",      "مناسبات",   "gift",       4),
            ("vehicles",    "مركبات",    "truck",      5),
            ("camps",       "مخيمات",    "tent",       6),
        };
        var kindIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var newNodes = new List<TaxonomyNodeEntity>();
        var dirty = false;

        foreach (var k in kinds)
        {
            if (existingByCode.TryGetValue(k.Code, out var existingNode))
            {
                // مَوجود سَلَفاً — لكِن لَو كانَ مُعَطَّلاً (deactivated reconcile سابِق)
                // نُعيد تَفعيله.
                if (!existingNode.IsActive)
                {
                    existingNode.IsActive = true;
                    existingNode.UpdatedAt = now;
                    dirty = true;
                }
                kindIds[k.Code] = existingNode.Id;
                continue;
            }
            var id = Guid.NewGuid();
            kindIds[k.Code] = id;
            newNodes.Add(new TaxonomyNodeEntity
            {
                Id = id, CreatedAt = now,
                RootCode = root, ParentId = null,
                Code = k.Code, Name = k.Code, NameAr = k.NameAr,
                Icon = k.Icon, SortOrder = k.Sort, IsActive = true,
            });
        }

        // ② عُقَد المُستَوى الثاني (الفِئات الفِعلِيَّة). slug = Code لِيُطابِق
        // <c>IListing.PropertyType</c> الحالي ⇒ لا migration بَيانات.
        // <b>Reconcile</b>: لَو الـ leaf مَوجود لكِن ParentId مُختَلِف عَن
        // الـ Kind الحالي في الـ seed، نَنقُله. هذا يُحَلّ حالَة camp →
        // outdoor (قَديم) إلى camp → camps (جَديد) بِلا تَدَخُّل يَدَوي.
        var sort = 0;
        foreach (var c in Ejar.Domain.EjarSeed.Categories)
        {
            sort++;
            kindIds.TryGetValue(c.Kind, out var pid);
            var targetParent = pid == Guid.Empty ? (Guid?)null : pid;

            if (existingByCode.TryGetValue(c.Id, out var leaf))
            {
                if (leaf.ParentId != targetParent || !leaf.IsActive)
                {
                    leaf.ParentId  = targetParent;
                    leaf.IsActive  = true;
                    leaf.UpdatedAt = now;
                    dirty = true;
                }
                continue;
            }
            newNodes.Add(new TaxonomyNodeEntity
            {
                Id = Guid.NewGuid(), CreatedAt = now,
                RootCode = root,
                ParentId = targetParent,
                Code = c.Id, Name = c.Id, NameAr = c.Label,
                Icon = c.Emoji,
                SortOrder = sort, IsActive = true,
            });
        }

        // ③ Cleanup: kinds مَوجودَة في DB لكِن لَيست في الـ seed الحالي ⇒
        // نُعَطِّلها (لا حَذف لِنَحفَظ سَجِل التَدقيق). كُلّ كَيان لَه ابن
        // مَنقول لِأَب جَديد سَيَترُك الـ kind القَديم بِلا أَولاد —
        // إخفاؤه يُنَظِّف الشَجَرَة في الواجِهَة.
        var seedKindCodes = kinds.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in existingNodes.Where(n => n.ParentId == null))
        {
            if (!seedKindCodes.Contains(existing.Code) && existing.IsActive)
            {
                existing.IsActive  = false;
                existing.UpdatedAt = now;
                dirty = true;
            }
        }

        if (newNodes.Count > 0) db.TaxonomyNodes.AddRange(newNodes);
        if (newNodes.Count + (dirty ? 1 : 0) > 0)
            db.SaveChanges();
    }

    /// <summary>
    /// بذرة إضافيّة idempotent لإصدارات التطبيق. تعمل عند كلّ بدء تشغيل لتغطّي
    /// قواعد البيانات القديمة. تضيف فقط <c>(platform, "1.0.0")</c> غير الموجودة.
    /// تستدعي <see cref="EnsureAppVersionsTable"/> أوّلاً لضمان وجود الجدول
    /// (DBs قديمة أُنشئت بـ EnsureCreated قبل إضافة AppVersionEntity لا تحتويه).
    /// </summary>
    public static void SeedAppVersionsIfMissing(EjarDbContext db)
    {
        EnsureAppVersionsTable(db);
        // 6. Seed App Versions
        // الإصدار الحاليّ "1.0.0" يُسجَّل بحالة Latest لكلّ منصّة. لا نسجّل
        // إصدارات قديمة هنا — السياسة الافتراضيّة في StoreBackedAppVersionGate
        // هي Lenient: أيّ إصدار غير مسجَّل يُعامَل كـ Active (يُسمح به). عند
        // نضوج النظام: سجّل كلّ إصدار قديم بحالته الفعليّة (Deprecated/
        // Unsupported) ثمّ بدّل السياسة إلى Strict عبر خيارات AddVersionsKit.
        const string downloadBase = "https://ejar.ye/download";
        var versionStatusLatest = (int)ACommerce.Kits.Versions.Operations.VersionStatus.Latest;
        var addedAny = false;
        foreach (var (platform, suffix) in new[] {
            ("web",    ""),
            ("wasm",   ""),
            ("mobile", "/mobile"),
            ("admin",  "/admin"),
        })
        {
            var exists = db.AppVersions.Any(v => v.Platform == platform && v.Version == "1.0.0");
            if (exists) continue;
            db.AppVersions.Add(new AppVersionEntity
            {
                Id          = Guid.NewGuid(),
                CreatedAt   = DateTime.UtcNow,
                Platform    = platform,
                Version     = "1.0.0",
                Status      = versionStatusLatest,
                DownloadUrl = downloadBase + suffix,
            });
            addedAny = true;
        }
        if (addedAny) db.SaveChanges();
    }

    /// <summary>
    /// يضمن وجود جدول <c>AppVersions</c> في قاعدة البيانات. حلّ سريع للقواعد
    /// التي أُنشئت بـ <c>EnsureCreated()</c> قبل إضافة <see cref="AppVersionEntity"/>
    /// — لا تحوي <c>__EFMigrationsHistory</c> فلا تستفيد من <c>Migrate()</c>
    /// (الذي سيحاول إنشاء كلّ الجداول ويفشل لأنّها موجودة).
    ///
    /// <para>الحلّ المثاليّ مستقبلاً: bootstrap لـ <c>__EFMigrationsHistory</c> ثمّ
    /// <c>Migrate()</c> للجديد فقط. هذا الـ helper الحاليّ يُكتفى به لتشغيل
    /// الإنتاج الآن.</para>
    /// </summary>
    public static void EnsureAppVersionsTable(EjarDbContext db)
    {
        var provider = db.Database.ProviderName ?? "";
        var sql = provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
            ? SqliteCreateTable
            : SqlServerCreateTable;
        try
        {
            db.Database.ExecuteSqlRaw(sql);
        }
        catch (Exception)
        {
            // فشل غير قاتل — قد يفشل DDL على بعض الإعدادات. لو الجدول لم يُنشأ
            // فعلاً، الاستعلام التالي سيرمي خطأً واضحاً للـ caller.
        }
    }

    private const string SqliteCreateTable = @"
CREATE TABLE IF NOT EXISTS ""AppVersions"" (
    ""Id""          TEXT    NOT NULL CONSTRAINT ""PK_AppVersions"" PRIMARY KEY,
    ""CreatedAt""   TEXT    NOT NULL,
    ""UpdatedAt""   TEXT    NULL,
    ""IsDeleted""   INTEGER NOT NULL DEFAULT 0,
    ""Platform""    TEXT    NOT NULL,
    ""Version""     TEXT    NOT NULL,
    ""Status""      INTEGER NOT NULL,
    ""SunsetAt""    TEXT    NULL,
    ""Notes""       TEXT    NULL,
    ""DownloadUrl"" TEXT    NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_AppVersions_Platform_Version""
    ON ""AppVersions"" (""Platform"", ""Version"");";

    private const string SqlServerCreateTable = @"
IF OBJECT_ID(N'[AppVersions]', N'U') IS NULL
BEGIN
    CREATE TABLE [AppVersions] (
        [Id]          UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_AppVersions] PRIMARY KEY,
        [CreatedAt]   DATETIME2        NOT NULL,
        [UpdatedAt]   DATETIME2        NULL,
        [IsDeleted]   BIT              NOT NULL DEFAULT 0,
        [Platform]    NVARCHAR(20)     NOT NULL,
        [Version]     NVARCHAR(40)     NOT NULL,
        [Status]      INT              NOT NULL,
        [SunsetAt]    DATETIME2        NULL,
        [Notes]       NVARCHAR(MAX)    NULL,
        [DownloadUrl] NVARCHAR(MAX)    NULL
    );
    CREATE UNIQUE INDEX [IX_AppVersions_Platform_Version]
        ON [AppVersions] ([Platform], [Version]);
END";
}
