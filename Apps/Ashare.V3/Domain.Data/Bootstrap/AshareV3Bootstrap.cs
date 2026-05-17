using Ashare.V3.Data;
using Ashare.V3.Data.Templates;
using Ashare.V3.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ashare.V3.Bootstrap;

/// <summary>
/// إقلاع آمِن لِـ Ashare V3. <b>لا</b> يَستَدعي <c>Database.Migrate()</c>
/// كامِلاً ولا <c>EnsureCreated()</c> عَلى production DB — لِأَنّ asharedb
/// مَملوء بِبَيانات حَيَّة بِـ schema سابِق عَلى V3.
///
/// <para>الذي يَفعَله:</para>
/// <list type="bullet">
///   <item>يَفحَص اتِّصال DB</item>
///   <item>لِكُلّ جَدول جَديد لِـ V3 (<c>Favorites</c>, <c>Reports</c>,
///         <c>Notifications</c>) يُنَفِّذ <c>CREATE TABLE IF NOT EXISTS</c>
///         (SQL Server: <c>IF OBJECT_ID(...) IS NULL CREATE TABLE ...</c>)</item>
///   <item>لا يَلمَس أَيّ جَدول قائِم</item>
/// </list>
///
/// <para><b>لا تُمَكِّن EF auto-migration في asharedb مَهما حَدَث.</b></para>
/// </summary>
public static class AshareV3Bootstrap
{
    /// <summary>
    /// قائِمَة الـ IPs/hosts المَحظورَة في dev/staging. الـ Bootstrap يَرفُض الإقلاع
    /// إن وُجِدَ أَيّ مِنها في ConnectionString — حِماية ضِدّ كِتابَة عَن طَريق
    /// الخَطَأ في إنتاج. حَدِّث القائِمَة لَو تَغَيَّر IP الإنتاج.
    /// </summary>
    private static readonly string[] ProductionHostMarkers =
    [
        "34.166.82.42",   // asharedb prod (GCP)
    ];

    public static async Task EnsureSchemaAsync(
        IServiceProvider sp,
        IConfiguration config,
        CancellationToken ct = default)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AshareV3DbContext>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()
                          ?.CreateLogger("Ashare.V3.Bootstrap");

        // ⓪ Guard: لا تَتَّصِل بِإنتاج مِن dev API. الـ override الوَحيد
        // المَسموح: env var ASHAREV3_ALLOW_PROD_CONN=true (لا تَستَخدِمه).
        var cs = config["Database:ConnectionString"] ?? "";
        var allowProd = string.Equals(
            Environment.GetEnvironmentVariable("ASHAREV3_ALLOW_PROD_CONN"),
            "true", StringComparison.OrdinalIgnoreCase);
        foreach (var marker in ProductionHostMarkers)
        {
            if (cs.Contains(marker, StringComparison.OrdinalIgnoreCase) && !allowProd)
            {
                var msg = $"Ashare V3 يَرفُض الإقلاع: ConnectionString يَحوي host إنتاج ({marker}). " +
                          $"اِستَنسَخ القاعِدَة مَحَلِّيّاً (راجع Apps/Ashare.V3/Tools/CloneAshareDb) " +
                          $"وحَوِّل appsettings.Development.json إلى SQLite. " +
                          $"لِتَجاوُز هذا الفَحص في حالات اضطِرارِيَّة فَقَط، ضَع " +
                          $"ASHAREV3_ALLOW_PROD_CONN=true.";
                logger?.LogCritical(msg);
                throw new InvalidOperationException(msg);
            }
        }

        // ① اتِّصال
        var isSqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

        // SQLite: تَأَكَّد مِن وُجود مُجَلَّد Data/ قَبل أَيّ شَيء.
        if (isSqlite)
        {
            var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
        }

        try
        {
            var ok = await db.Database.CanConnectAsync(ct);
            if (!ok && !isSqlite)
                throw new InvalidOperationException("CanConnectAsync returned false");
            logger?.LogInformation("Ashare V3: connected to DB (provider={Provider})", db.Database.ProviderName);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ashare V3: DB connection failed — abort");
            throw;
        }

        // ② Schema
        if (isSqlite)
        {
            // SQLite (dev): EnsureCreated يَبني كُلّ الجَداوِل مِن EF model.
            // آمِن لِأَنّ القاعِدَة مَحَلِّيَّة، لَو فارِغَة تُملَأ بِـ clone tool،
            // ولَو مُمتَلِئَة بَعد clone EnsureCreated لا يَلمَس شَيئاً (idempotent).
            await db.Database.EnsureCreatedAsync(ct);

            // طَبَعَ المَسار المُطلَق + عَدَد صُفوف ProductListing لِيَكتَشِف
            // المُطَوِّر فَوراً ما إذا كانَت القاعِدَة فارِغَة (يَحتاج clone)
            // أَو عَلى مَسار مُختَلِف عَن الَّذي كَتَبَ إلَيه استِنساخ سابِق.
            var sqliteConn = db.Database.GetDbConnection();
            var dbPath = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(sqliteConn.ConnectionString).DataSource;
            var listingCount = await db.ProductListings.IgnoreQueryFilters().CountAsync(ct);
            var profileCount = await db.Profiles.IgnoreQueryFilters().CountAsync(ct);
            logger?.LogInformation(
                "Ashare V3: SQLite schema ensured. Path={Path} | ProductListing={L} Profile={P}",
                dbPath, listingCount, profileCount);
            if (listingCount == 0)
            {
                logger?.LogWarning(
                    "Ashare V3: ProductListing فارِغ. شَغِّل أَداة الاستِنساخ: " +
                    "ASHAREDB_PROD_CONN='…' dotnet run --project Apps/Ashare.V3/Tools/CloneAshareDb");
            }
            // لا seed قَوالِب فِئات المُنتَجات مَن الكود — تَسميات سِمات
            // الإعلانات وخِياراتها تَأتي مِن جَداوِل asharedb
            // (AttributeDefinitions/Values/CategoryAttributeMappings).
            // الإدارَة تَملَأها عَبر admin/SQL أَو لوحَة تَحَكُّم لاحِقَة.
            //
            // الاستِثناء الوَحيد: سِمات البروفايل. كُلّ ما لَيس في واجِهَة
            // <see cref="IUserProfile"/> ينقُله Clone إلى <c>AttributesJson</c>
            // (BusinessName, Type, IsActive, IsVerified, VerifiedAt, Address,
            // Country, PostalCode, Coordinates). نُسَجِّل تَعريفات هذه
            // المَفاتيح كَـ <c>AttributeDefinitions</c> تَحت sentinel
            // <see cref="V3ProfileAttributes.CategoryId"/> لِيُعيد
            // <c>ProductionAttributeTemplateSource</c> بِناءَها كَـ template
            // ⇒ نَفس مَحَرِّك القَوالِب يَعمَل عَلى البروفايل.
            await SeedProfileAttributesAsync(db, logger, ct);
            await SeedRoommateCategoriesAsync(db, logger, ct);
            await SeedTaxonomyNodesAsync(db, logger, ct);
            await SeedSaudiCitiesAsync(db, logger, ct);
            await MaybeWipeLegacyListingsAsync(db, logger, ct);
        }
        else
        {
            // SQL Server: additive فَقَط. لا EnsureCreated مَهما حَدَث.
            foreach (var stmt in SqlServerNewTables)
            {
                try
                {
                    await db.Database.ExecuteSqlRawAsync(stmt, ct);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Ashare V3: optional table creation skipped: {Stmt}",
                                       stmt.Length > 80 ? stmt[..80] + "…" : stmt);
                }
            }
            // الـ seeds تَركَض بَعد التَّأَكُّد مَن وُجود الجَداوِل. idempotent
            // كُلّها فَلا أَثَر سَلبي لَو رُكِضَت مَرَّة بَعد أُخرى.
            await SeedProfileAttributesAsync(db, logger, ct);
            await SeedRoommateCategoriesAsync(db, logger, ct);
            await SeedTaxonomyNodesAsync(db, logger, ct);
            await SeedSaudiCitiesAsync(db, logger, ct);
            await MaybeWipeLegacyListingsAsync(db, logger, ct);
            logger?.LogInformation("Ashare V3: SQL Server additive schema check complete");
        }
    }

    /// <summary>
    /// تَنظيف اختياري لِلعُروض القَديمَة المَنقولَة مَن V2 الإنتاج.
    /// مَحروس بِـ env var <c>ASHAREV3_WIPE_LISTINGS=true</c> — لا يَعمَل
    /// تِلقائيّاً. السَبَب: صُوَر V2 (<c>api.ashare.sa/media/...</c>) لَم
    /// تَعُد يُمكِن الوُصول إلَيها (host مُعَلَّق). الصاحِب يُفَعِّل المُتَغَيِّر
    /// لِبَدء طازَج: <c>SET ASHAREV3_WIPE_LISTINGS=true</c> ⇒ يُعيد التَّشغيل
    /// مَرَّة ⇒ يُلغي المُتَغَيِّر.
    ///
    /// <para>الحَذف soft — <c>IsDeleted = true</c> + <c>UpdatedAt = utcNow</c>،
    /// المُستَخدِمون يَبقَون كَما هُم (لِتَجارِب أَكثَر واقِعِيَّة، طَلَب صَريح
    /// مَن المالِك). لا يَلمَس Profiles/Chats/Favorites.</para>
    /// </summary>
    private static async Task MaybeWipeLegacyListingsAsync(
        AshareV3DbContext db, ILogger? logger, CancellationToken ct)
    {
        var wipe = string.Equals(
            Environment.GetEnvironmentVariable("ASHAREV3_WIPE_LISTINGS"),
            "true", StringComparison.OrdinalIgnoreCase);
        if (!wipe) return;

        try
        {
            var now = DateTime.UtcNow;
            var rows = await db.ProductListings.IgnoreQueryFilters()
                .Where(l => !l.IsDeleted).ToListAsync(ct);
            if (rows.Count == 0)
            {
                logger?.LogInformation("Ashare V3: ASHAREV3_WIPE_LISTINGS=true but nothing to wipe");
                return;
            }
            foreach (var r in rows)
            {
                r.IsDeleted = true;
                r.UpdatedAt = now;
            }
            await db.SaveChangesAsync(ct);
            logger?.LogWarning(
                "Ashare V3: WIPED {Count} ProductListing rows (ASHAREV3_WIPE_LISTINGS=true). " +
                "Unset the env var before next start.", rows.Count);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ashare V3: legacy listings wipe failed");
        }
    }

    // SQL Server: IF OBJECT_ID('dbo.Foo', 'U') IS NULL CREATE TABLE …
    private static readonly string[] SqlServerNewTables = new[]
    {
        @"IF OBJECT_ID('dbo.Favorites', 'U') IS NULL
          CREATE TABLE [dbo].[Favorites] (
            [Id]         uniqueidentifier NOT NULL PRIMARY KEY,
            [CreatedAt]  datetime2        NOT NULL,
            [UpdatedAt]  datetime2        NULL,
            [IsDeleted]  bit              NOT NULL,
            [UserId]     nvarchar(450)    NOT NULL,
            [ListingId]  uniqueidentifier NOT NULL,
            CONSTRAINT [UQ_Favorites_UserId_ListingId] UNIQUE ([UserId], [ListingId])
          );",

        @"IF OBJECT_ID('dbo.Reports', 'U') IS NULL
          CREATE TABLE [dbo].[Reports] (
            [Id]              uniqueidentifier NOT NULL PRIMARY KEY,
            [CreatedAt]       datetime2        NOT NULL,
            [UpdatedAt]       datetime2        NULL,
            [IsDeleted]       bit              NOT NULL,
            [ReporterId]      nvarchar(450)    NOT NULL,
            [EntityType]      nvarchar(100)    NOT NULL,
            [EntityId]        uniqueidentifier NOT NULL,
            [Reason]          nvarchar(200)    NOT NULL,
            [Description]     nvarchar(2000)   NULL,
            [Status]          nvarchar(50)     NOT NULL,
            [ResolvedAt]      datetime2        NULL,
            [ResolvedById]    nvarchar(450)    NULL,
            [ResolutionNotes] nvarchar(2000)   NULL
          );",

        @"IF OBJECT_ID('dbo.Notifications', 'U') IS NULL
          CREATE TABLE [dbo].[Notifications] (
            [Id]           uniqueidentifier NOT NULL PRIMARY KEY,
            [CreatedAt]    datetime2        NOT NULL,
            [UpdatedAt]    datetime2        NULL,
            [IsDeleted]    bit              NOT NULL,
            [UserId]       nvarchar(450)    NOT NULL,
            [Title]        nvarchar(500)    NOT NULL,
            [Body]         nvarchar(max)    NOT NULL,
            [Kind]         nvarchar(50)     NOT NULL,
            [IsRead]       bit              NOT NULL,
            [ReadAt]       datetime2        NULL,
            [DeepLinkUrl]  nvarchar(500)    NULL,
            [MetadataJson] nvarchar(max)    NULL
          );",

        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notifications_UserId_IsRead')
          CREATE INDEX [IX_Notifications_UserId_IsRead] ON [dbo].[Notifications] ([UserId], [IsRead]);",

        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reports_EntityType_EntityId')
          CREATE INDEX [IX_Reports_EntityType_EntityId] ON [dbo].[Reports] ([EntityType], [EntityId]);",

        @"IF OBJECT_ID('dbo.DiscoveryCategories', 'U') IS NULL
          CREATE TABLE [dbo].[DiscoveryCategories] (
            [Id]        uniqueidentifier NOT NULL PRIMARY KEY,
            [CreatedAt] datetime2        NOT NULL,
            [UpdatedAt] datetime2        NULL,
            [IsDeleted] bit              NOT NULL,
            [Slug]      nvarchar(100)    NOT NULL,
            [Label]     nvarchar(100)    NOT NULL,
            [Icon]      nvarchar(50)     NOT NULL,
            [Kind]      nvarchar(50)     NOT NULL
          );",

        @"IF OBJECT_ID('dbo.DiscoveryRegions', 'U') IS NULL
          CREATE TABLE [dbo].[DiscoveryRegions] (
            [Id]        uniqueidentifier NOT NULL PRIMARY KEY,
            [CreatedAt] datetime2        NOT NULL,
            [UpdatedAt] datetime2        NULL,
            [IsDeleted] bit              NOT NULL,
            [Name]      nvarchar(100)    NOT NULL,
            [ParentId]  uniqueidentifier NULL,
            [Level]     int              NOT NULL
          );",

        @"IF OBJECT_ID('dbo.DiscoveryAmenities', 'U') IS NULL
          CREATE TABLE [dbo].[DiscoveryAmenities] (
            [Id]        uniqueidentifier NOT NULL PRIMARY KEY,
            [CreatedAt] datetime2        NOT NULL,
            [UpdatedAt] datetime2        NULL,
            [IsDeleted] bit              NOT NULL,
            [Slug]      nvarchar(50)     NOT NULL,
            [Label]     nvarchar(100)    NOT NULL
          );",

        // TaxonomyNodes — مَوحَّدَة مَع إيجار. شَجَرَة "listing_categories" =
        // مَصدَر الفِئات في Home/Explore/CreateListing.
        @"IF OBJECT_ID('dbo.TaxonomyNodes', 'U') IS NULL
          CREATE TABLE [dbo].[TaxonomyNodes] (
            [Id]        uniqueidentifier NOT NULL PRIMARY KEY,
            [CreatedAt] datetime2        NOT NULL,
            [UpdatedAt] datetime2        NULL,
            [IsDeleted] bit              NOT NULL,
            [ParentId]  uniqueidentifier NULL,
            [RootCode]  nvarchar(60)     NOT NULL,
            [Code]      nvarchar(80)     NOT NULL,
            [Name]      nvarchar(120)    NOT NULL,
            [NameAr]    nvarchar(120)    NULL,
            [Icon]      nvarchar(40)     NULL,
            [SortOrder] int              NOT NULL,
            [IsActive]  bit              NOT NULL
          );",

        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TaxonomyNodes_RootCode_Code')
          CREATE UNIQUE INDEX [IX_TaxonomyNodes_RootCode_Code] ON [dbo].[TaxonomyNodes] ([RootCode], [Code]);",

        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TaxonomyNodes_RootCode_ParentId_SortOrder')
          CREATE INDEX [IX_TaxonomyNodes_RootCode_ParentId_SortOrder] ON [dbo].[TaxonomyNodes] ([RootCode], [ParentId], [SortOrder]);",

        // ProductListing — أَعمِدَة أُضيفَت في V3 لِتُغَطّي <see cref="IListing"/>
        // (TimeUnit/BedroomCount/BathroomCount/AreaSqm/AmenitiesJson). تَطبيقات
        // V3 المُتَّصِلَة بِـ asharedb V2 (runasp.net) لا تَملِكها بَعد ⇒ EF
        // يَفشَل بِـ "Invalid column name". الإضافَة آمِنَة (NULL DEFAULT).
        @"IF COL_LENGTH('dbo.ProductListing', 'TimeUnit') IS NULL
          ALTER TABLE [dbo].[ProductListing] ADD [TimeUnit] nvarchar(40) NULL;",

        @"IF COL_LENGTH('dbo.ProductListing', 'BedroomCount') IS NULL
          ALTER TABLE [dbo].[ProductListing] ADD [BedroomCount] int NOT NULL CONSTRAINT [DF_ProductListing_BedroomCount] DEFAULT 0;",

        @"IF COL_LENGTH('dbo.ProductListing', 'BathroomCount') IS NULL
          ALTER TABLE [dbo].[ProductListing] ADD [BathroomCount] int NOT NULL CONSTRAINT [DF_ProductListing_BathroomCount] DEFAULT 0;",

        @"IF COL_LENGTH('dbo.ProductListing', 'AreaSqm') IS NULL
          ALTER TABLE [dbo].[ProductListing] ADD [AreaSqm] int NOT NULL CONSTRAINT [DF_ProductListing_AreaSqm] DEFAULT 0;",

        @"IF COL_LENGTH('dbo.ProductListing', 'AmenitiesJson') IS NULL
          ALTER TABLE [dbo].[ProductListing] ADD [AmenitiesJson] nvarchar(max) NULL;",

        // OperationIdempotency — جَدول جَديد لِـ V3. يَفحَصه
        // <c>IdempotencyInterceptor</c> قَبل كُلّ كِتابَة لِمَنع إعادَة
        // التَنفيذ نَفس <c>idempotency_key</c>.
        @"IF OBJECT_ID('dbo.OperationIdempotency', 'U') IS NULL
          CREATE TABLE [dbo].[OperationIdempotency] (
            [Id]            uniqueidentifier NOT NULL PRIMARY KEY,
            [CreatedAt]     datetime2        NOT NULL,
            [UpdatedAt]     datetime2        NULL,
            [IsDeleted]     bit              NOT NULL,
            [Key]           nvarchar(64)     NOT NULL,
            [OperationType] nvarchar(120)    NOT NULL,
            [Snapshot]      nvarchar(200)    NOT NULL
          );",

        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OperationIdempotency_Key')
          CREATE UNIQUE INDEX [IX_OperationIdempotency_Key] ON [dbo].[OperationIdempotency] ([Key]);",

        // CategoryAttributeTemplates — جَدول V3-additive لِخَدمَة قَوالِب
        // فِئات admin-edited (JSON طازَج لِسلاجات بِلا CategoryAttributeMappings).
        @"IF OBJECT_ID('dbo.CategoryAttributeTemplates', 'U') IS NULL
          CREATE TABLE [dbo].[CategoryAttributeTemplates] (
            [Id]           uniqueidentifier NOT NULL PRIMARY KEY,
            [CreatedAt]    datetime2        NOT NULL,
            [UpdatedAt]    datetime2        NULL,
            [IsDeleted]    bit              NOT NULL,
            [CategorySlug] nvarchar(100)    NOT NULL,
            [TemplateJson] nvarchar(max)    NOT NULL,
            [CodeVersion]  int              NOT NULL
          );",

        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CategoryAttributeTemplates_CategorySlug')
          CREATE UNIQUE INDEX [IX_CategoryAttributeTemplates_CategorySlug] ON [dbo].[CategoryAttributeTemplates] ([CategorySlug]);",

        // ListingPayments — جَدول V3-additive (لا باقات اشتِراك في V3،
        // الدَفع لِلإعلان الواحِد). ListingPaymentGateInterceptor يَفحَصه.
        @"IF OBJECT_ID('dbo.ListingPayments', 'U') IS NULL
          CREATE TABLE [dbo].[ListingPayments] (
            [Id]         uniqueidentifier NOT NULL PRIMARY KEY,
            [CreatedAt]  datetime2        NOT NULL,
            [UpdatedAt]  datetime2        NULL,
            [IsDeleted]  bit              NOT NULL,
            [UserId]     nvarchar(450)    NOT NULL,
            [ListingId]  uniqueidentifier NULL,
            [Provider]   nvarchar(60)     NOT NULL,
            [Reference]  nvarchar(120)    NOT NULL,
            [Amount]     decimal(18,2)    NOT NULL,
            [Currency]   nvarchar(10)     NOT NULL,
            [Status]     nvarchar(40)     NOT NULL,
            [Consumed]   bit              NOT NULL,
            [CapturedAt] datetime2        NULL
          );",

        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ListingPayments_UserId_Status')
          CREATE INDEX [IX_ListingPayments_UserId_Status] ON [dbo].[ListingPayments] ([UserId], [Status]);",

        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ListingPayments_Reference')
          CREATE UNIQUE INDEX [IX_ListingPayments_Reference] ON [dbo].[ListingPayments] ([Reference]);"
    };

    /// <summary>
    /// يَضمَن وُجود <c>AttributeDefinitions</c> + <c>CategoryAttributeMappings</c>
    /// لِأَعمِدَة بروفايل V3 الإضافِيَّة تَحت
    /// <see cref="V3ProfileAttributes.CategoryId"/> sentinel. idempotent:
    /// يَفحَص الـ Code قَبل الإضافَة، ولا يَلمَس صُفوف الإنتاج (الـ
    /// CategoryId مُختَلِق ⇒ لا يَتَعارَض مَع mappings فِئات حَقيقِيَّة).
    /// </summary>
    private static async Task SeedProfileAttributesAsync(
        AshareV3DbContext db, ILogger? logger, CancellationToken ct)
    {
        try
        {
            var sentinelCat = V3ProfileAttributes.CategoryId;
            // EF query: نُجَسِّد الـ codes إلى array قَبل الـ Where —
            // <c>Defaults.Select(...).Contains</c> داخِل الـ tree قَد لا
            // يُتَرجَم بِشَكل مَوثوق (يَفشَل بِصَمت في بَعض الـ EF runtimes)
            // ⇒ الـ defs الجَديدَة لا تُلتَقَط ⇒ Seed لا يُنشِئ شَيئاً.
            var defaultCodes = V3ProfileAttributes.Defaults.Select(x => x.Code).ToArray();
            var existing = await db.AttributeDefinitions.AsNoTracking()
                .Where(d => defaultCodes.Contains(d.Code))
                .ToDictionaryAsync(d => d.Code, d => d.Id, ct);

            var now = DateTime.UtcNow;
            var newDefs = new List<AttributeDefinitionEntity>();
            var newMaps = new List<CategoryAttributeMappingEntity>();

            var existingMappedDefIds = await db.CategoryAttributeMappings.AsNoTracking()
                .Where(m => m.CategoryId == sentinelCat)
                .Select(m => m.AttributeDefinitionId)
                .ToListAsync(ct);
            var mappedSet = existingMappedDefIds.ToHashSet();

            var sort = 0;
            foreach (var seed in V3ProfileAttributes.Defaults)
            {
                sort++;
                Guid defId;
                if (!existing.TryGetValue(seed.Code, out defId))
                {
                    defId = Guid.NewGuid();
                    newDefs.Add(new AttributeDefinitionEntity
                    {
                        Id        = defId,
                        CreatedAt = now,
                        Code      = seed.Code,
                        Name      = seed.Name,
                        Type      = seed.Type,
                        IsRequired       = false,
                        IsVisibleInList  = false,
                        IsVisibleInDetail = true,
                        SortOrder = sort,
                    });
                }
                if (!mappedSet.Contains(defId))
                {
                    newMaps.Add(new CategoryAttributeMappingEntity
                    {
                        Id        = Guid.NewGuid(),
                        CreatedAt = now,
                        CategoryId            = sentinelCat,
                        AttributeDefinitionId = defId,
                        SortOrder             = sort,
                        IsActive              = true,
                    });
                }
            }

            if (newDefs.Count > 0) db.AttributeDefinitions.AddRange(newDefs);
            if (newMaps.Count > 0) db.CategoryAttributeMappings.AddRange(newMaps);
            if (newDefs.Count + newMaps.Count > 0)
                await db.SaveChangesAsync(ct);

            // مُلَخَّص حالَة الـ seed لِلتَشخيص — العَدَد النِهائي لِـ mappings
            // عَلى sentinel scope هو ما يَستَخدِمه <c>BuildForScopeAsync</c>
            // لِبِناء قالَب الـ Profile. صِفر هُنا = صَفحَة /profile/edit
            // تَقول "لا سِمات".
            var finalMappingCount = await db.CategoryAttributeMappings
                .CountAsync(m => m.CategoryId == sentinelCat && m.IsActive, ct);
            logger?.LogInformation(
                "Ashare V3: profile attribute seed → +{Defs} defs, +{Maps} mappings; total active mappings on profile scope = {Total}",
                newDefs.Count, newMaps.Count, finalMappingCount);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Ashare V3: profile attribute seed skipped");
        }
    }

    /// <summary>
    /// يَزرَع فِئَتَي الـ roommate ("عَنده سَكَن" / "يَدور سَكَن") + كُلّ
    /// سِماتهما الديناميكِيَّة في DB. idempotent تَماماً: يُحَدِّث مَوجود +
    /// يُضيف ناقِص بِلا لَمس بَيانات أُخرى.
    ///
    /// <para>المَنطِق:
    /// <list type="number">
    ///   <item><b>UPSERT</b> صَفَّي <see cref="ProductCategoryEntity"/> عَلى Id
    ///         (Guid ثابِت في <see cref="AshareV3RoommateAttributes"/>).</item>
    ///   <item><b>UPSERT</b> <see cref="AttributeDefinitionEntity"/> لِكُلّ
    ///         <c>Code</c> فَريد — تَشارُك <c>BedroomShare</c>/<c>Smoking</c> بَين
    ///         الفِئَتَين يَستَخدِم نَفس الـ definition.</item>
    ///   <item><b>UPSERT</b> <see cref="AttributeValueEntity"/> لِكُلّ خِيار
    ///         في select-like definitions (المُفتاح: DefinitionId + Value).</item>
    ///   <item><b>UPSERT</b> <see cref="CategoryAttributeMappingEntity"/>
    ///         لِكُلّ رِبط (Category, Definition) — هو ما يَفحَصه
    ///         <see cref="ProductionAttributeTemplateSource"/>.</item>
    /// </list></para>
    ///
    /// <para>بَعد هذا الـ seed، الـ controller <c>/categories/{slug}/attribute-template</c>
    /// يَرُدّ القالَب مَن DB طَبيعِيّاً ⇒ لا حاجَة لِفَرع الـ in-memory.
    /// لوحَة الإدارَة (مُستَقبَلِيَّة) تَستَطيع تَعديل الحُقول مُباشَرَة
    /// لِأَنّها الآن صُفوف DB.</para>
    /// </summary>
    private static async Task SeedRoommateCategoriesAsync(
        AshareV3DbContext db, ILogger? logger, CancellationToken ct)
    {
        try
        {
            var now = DateTime.UtcNow;

            // ─── ① الفِئَتان ─────────────────────────────────────────────
            await UpsertCategoryAsync(db,
                id:   AshareV3RoommateAttributes.RoommateHasCategoryId,
                slug: AshareV3RoommateAttributes.RoommateHasSlug,
                name: AshareV3RoommateAttributes.RoommateHasName,
                icon: "🏠", sortOrder: 1, now: now, ct: ct);

            await UpsertCategoryAsync(db,
                id:   AshareV3RoommateAttributes.RoommateWantsCategoryId,
                slug: AshareV3RoommateAttributes.RoommateWantsSlug,
                name: AshareV3RoommateAttributes.RoommateWantsName,
                icon: "🔍", sortOrder: 2, now: now, ct: ct);

            await db.SaveChangesAsync(ct);

            // ─── ② تَجميع كُلّ الـ codes الفَريدَة عَبر الفِئَتَين ───────
            var allSeeds = AshareV3RoommateAttributes.RoommateHasFields
                .Concat(AshareV3RoommateAttributes.RoommateWantsFields)
                .GroupBy(s => s.Code, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First()); // أَوَّل تَعريف يَفوز عِندَ التَشارُك

            // ─── ③ AttributeDefinitions: UPSERT بِالـ Code ───────────────
            var existingDefs = await db.AttributeDefinitions.AsNoTracking()
                .Where(d => allSeeds.Keys.Contains(d.Code))
                .ToDictionaryAsync(d => d.Code, d => d, ct);

            var defIdByCode = new Dictionary<string, Guid>(StringComparer.Ordinal);
            var newDefs = new List<AttributeDefinitionEntity>();
            var sortGlobal = 0;
            foreach (var (code, seed) in allSeeds)
            {
                sortGlobal++;
                if (existingDefs.TryGetValue(code, out var existing))
                {
                    defIdByCode[code] = existing.Id;
                    // لا نَلمَس صُفوفاً قائِمَة — قَد تَكون لوحَة الإدارَة
                    // عَدَّلتها (Name/Description/IsRequired). الـ seed
                    // يَمنَح وُجوداً، الإدارَة تَملِك التَّعديل.
                    continue;
                }
                var id = Guid.NewGuid();
                defIdByCode[code] = id;
                newDefs.Add(new AttributeDefinitionEntity
                {
                    Id        = id,
                    CreatedAt = now,
                    Code      = seed.Code,
                    Name      = seed.Name,
                    Type      = seed.Type,
                    IsRequired       = false,
                    IsFilterable     = false,
                    IsVisibleInList  = false,
                    IsVisibleInDetail = true,
                    SortOrder = sortGlobal,
                });
            }
            if (newDefs.Count > 0) db.AttributeDefinitions.AddRange(newDefs);
            await db.SaveChangesAsync(ct);

            // ─── ④ AttributeValues لِلـ select-like ────────────────────
            var defIdsWithOptions = allSeeds.Values
                .Where(s => s.Options is { Length: > 0 })
                .Select(s => defIdByCode[s.Code])
                .ToList();
            var existingValues = await db.AttributeValues.AsNoTracking()
                .Where(v => defIdsWithOptions.Contains(v.AttributeDefinitionId))
                .ToListAsync(ct);
            var existingValueKeys = existingValues
                .Select(v => (v.AttributeDefinitionId, v.Value))
                .ToHashSet();

            var newValues = new List<AttributeValueEntity>();
            foreach (var seed in allSeeds.Values)
            {
                if (seed.Options is not { Length: > 0 }) continue;
                var defId = defIdByCode[seed.Code];
                var optSort = 0;
                foreach (var opt in seed.Options)
                {
                    optSort++;
                    if (existingValueKeys.Contains((defId, opt.Value))) continue;
                    newValues.Add(new AttributeValueEntity
                    {
                        Id        = Guid.NewGuid(),
                        CreatedAt = now,
                        AttributeDefinitionId = defId,
                        Value       = opt.Value,
                        DisplayName = opt.LabelAr,
                        SortOrder   = optSort,
                        IsActive    = true,
                    });
                }
            }
            if (newValues.Count > 0) db.AttributeValues.AddRange(newValues);
            await db.SaveChangesAsync(ct);

            // ─── ⑤ CategoryAttributeMappings ───────────────────────────
            await UpsertMappingsAsync(db,
                categoryId: AshareV3RoommateAttributes.RoommateHasCategoryId,
                fields:     AshareV3RoommateAttributes.RoommateHasFields,
                defIdByCode: defIdByCode, now: now, ct: ct);

            await UpsertMappingsAsync(db,
                categoryId: AshareV3RoommateAttributes.RoommateWantsCategoryId,
                fields:     AshareV3RoommateAttributes.RoommateWantsFields,
                defIdByCode: defIdByCode, now: now, ct: ct);

            await db.SaveChangesAsync(ct);

            var hasMaps = await db.CategoryAttributeMappings
                .CountAsync(m => m.CategoryId == AshareV3RoommateAttributes.RoommateHasCategoryId
                                 && m.IsActive, ct);
            var wantsMaps = await db.CategoryAttributeMappings
                .CountAsync(m => m.CategoryId == AshareV3RoommateAttributes.RoommateWantsCategoryId
                                 && m.IsActive, ct);
            logger?.LogInformation(
                "Ashare V3: roommate seed → +{Defs} defs, +{Vals} values; mappings on roommate_has={H}, roommate_wants={W}",
                newDefs.Count, newValues.Count, hasMaps, wantsMaps);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Ashare V3: roommate category seed skipped");
        }
    }

    private static async Task UpsertCategoryAsync(AshareV3DbContext db,
        Guid id, string slug, string name, string icon, int sortOrder,
        DateTime now, CancellationToken ct)
    {
        var existing = await db.ProductCategories.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (existing is null)
        {
            db.ProductCategories.Add(new ProductCategoryEntity
            {
                Id        = id,
                CreatedAt = now,
                Slug      = slug,
                Name      = name,
                Icon      = icon,
                SortOrder = sortOrder,
                IsActive  = true,
                IsDeleted = false,
            });
        }
        else
        {
            // اِجبار عَلى صَلاحِيَّة الـ slug + الاسم لَو تَغَيَّر الـ seed.
            // لا نَلمَس Description/Image — لوحَة الإدارَة قَد تَكون أَدخَلَتها.
            var changed = false;
            if (existing.Slug != slug) { existing.Slug = slug; changed = true; }
            if (existing.Name != name) { existing.Name = name; changed = true; }
            if (string.IsNullOrEmpty(existing.Icon)) { existing.Icon = icon; changed = true; }
            if (!existing.IsActive)    { existing.IsActive = true; changed = true; }
            if (existing.IsDeleted)    { existing.IsDeleted = false; changed = true; }
            if (changed) existing.UpdatedAt = now;
        }
    }

    private static async Task UpsertMappingsAsync(AshareV3DbContext db,
        Guid categoryId,
        IReadOnlyList<AshareV3RoommateAttributes.AttrSeed> fields,
        Dictionary<string, Guid> defIdByCode,
        DateTime now, CancellationToken ct)
    {
        var existing = await db.CategoryAttributeMappings.AsNoTracking()
            .Where(m => m.CategoryId == categoryId)
            .ToListAsync(ct);
        var existingByDefId = existing.ToDictionary(m => m.AttributeDefinitionId);

        var sort = 0;
        foreach (var seed in fields)
        {
            sort++;
            if (!defIdByCode.TryGetValue(seed.Code, out var defId)) continue;
            if (existingByDefId.ContainsKey(defId)) continue;
            db.CategoryAttributeMappings.Add(new CategoryAttributeMappingEntity
            {
                Id        = Guid.NewGuid(),
                CreatedAt = now,
                CategoryId            = categoryId,
                AttributeDefinitionId = defId,
                SortOrder             = sort,
                IsActive              = true,
            });
        }
    }

    /// <summary>
    /// يَزرَع شَجَرَة <c>listing_categories</c> في <see cref="TaxonomyNodeEntity"/>:
    /// kind أَب <c>roommate</c> + leaf-ان <c>roommate_has</c> و <c>roommate_wants</c>.
    /// مَطابِق لِأُسلوب إيجار في <c>DbInitializer.SeedTaxonomyIfMissing</c>.
    ///
    /// <para>idempotent: لِكُلّ عُقدَة، يُحَدِّث الـ Name/NameAr/Icon/SortOrder
    /// لَو تَغَيَّر الـ seed، ويُعَطِّل الـ kinds القَديمَة الَّتي لَم تَعُد
    /// في الـ seed (مَثَل: لَو كانَت هُناك "apartment" قَديمَة). لا يَلمَس
    /// أَبناء غَير V3-managed.</para>
    /// </summary>
    private static async Task SeedTaxonomyNodesAsync(
        AshareV3DbContext db, ILogger? logger, CancellationToken ct)
    {
        try
        {
            const string root = "listing_categories";
            var now = DateTime.UtcNow;

            var existing = await db.TaxonomyNodes.IgnoreQueryFilters()
                .Where(t => t.RootCode == root)
                .ToListAsync(ct);
            var existingByCode = existing.ToDictionary(
                n => n.Code, n => n, StringComparer.OrdinalIgnoreCase);

            // الـ kind الأَب (Guid ثابِت يَطابِق <c>AshareV3TaxonomyStore</c>
            // القَديم — لَو كان أَحَد قَد ربَط بَيانات بِالـ Id فَلا يَنكَسِر).
            var roommateKindId = Guid.Parse("0a01a01a-0a01-0a01-0a01-0a01000a01a1");
            await UpsertTaxonomyNodeAsync(db, existingByCode,
                id: roommateKindId,
                rootCode: root, code: "roommate",
                name: "Roommate", nameAr: "سَكَن مُشتَرَك",
                icon: "users", parentId: null, sortOrder: 1, now: now);

            // الـ leaves — تَستَخدِم نَفس Guids الَّتي زَرَعناها في
            // ProductCategories لِيَتَّسِق الـ slug ⇔ scopeId.
            await UpsertTaxonomyNodeAsync(db, existingByCode,
                id: AshareV3RoommateAttributes.RoommateHasCategoryId,
                rootCode: root, code: AshareV3RoommateAttributes.RoommateHasSlug,
                name: "Has a room", nameAr: AshareV3RoommateAttributes.RoommateHasName,
                icon: "🏠", parentId: roommateKindId, sortOrder: 1, now: now);

            await UpsertTaxonomyNodeAsync(db, existingByCode,
                id: AshareV3RoommateAttributes.RoommateWantsCategoryId,
                rootCode: root, code: AshareV3RoommateAttributes.RoommateWantsSlug,
                name: "Looking for a room", nameAr: AshareV3RoommateAttributes.RoommateWantsName,
                icon: "🔍", parentId: roommateKindId, sortOrder: 2, now: now);

            // Cleanup: kinds (المُستَوى الأَوَّل) المَوجودَة في DB لكِن لَيست
            // في الـ seed ⇒ نُعَطِّلها (لا حَذف لِنَحفَظ سَجِل التَدقيق).
            // ChildNodes لِأَبٍ آخَر لا تُلمَس.
            var seedKindCodes = new[] { "roommate" }.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var n in existing.Where(n => n.ParentId == null))
            {
                if (!seedKindCodes.Contains(n.Code) && n.IsActive)
                {
                    n.IsActive  = false;
                    n.UpdatedAt = now;
                }
            }

            await db.SaveChangesAsync(ct);

            var count = await db.TaxonomyNodes
                .CountAsync(t => t.RootCode == root && t.IsActive, ct);
            logger?.LogInformation("Ashare V3: taxonomy seed → {Count} active nodes under '{Root}'", count, root);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Ashare V3: taxonomy seed skipped");
        }
    }

    /// <summary>
    /// يَزرَع <see cref="ACommerce.Kits.Discovery.Domain.DiscoveryRegion"/>
    /// بِمُدُن سُعودِيَّة. V3 سوق سُعودي بِخِلاف إيجار (يَمَني). idempotent
    /// per-name. لا يَحذِف صُفوفاً قائِمَة حَتّى لَو أَدخَلَها الأَدمِن يَدَوياً.
    /// </summary>
    private static async Task SeedSaudiCitiesAsync(
        AshareV3DbContext db, ILogger? logger, CancellationToken ct)
    {
        try
        {
            // قائِمَة المُدُن الرَئيسِيَّة في المَملَكَة + مُتَطابِقَة مَع
            // <c>AshareV3RoommateAttributes.PreferredCities</c>.
            var saudi = new[]
            {
                "الرياض", "جدة", "مكة المكرمة", "المدينة المنورة",
                "الدمام", "الخبر", "الظهران", "الطائف",
                "بريدة", "تبوك", "حائل", "أبها", "خميس مشيط",
                "نجران", "جازان", "ينبع",
            };

            var existing = await db.DiscoveryRegions.IgnoreQueryFilters()
                .Select(r => r.Name).ToListAsync(ct);
            var existingSet = existing.ToHashSet(StringComparer.Ordinal);

            var now = DateTime.UtcNow;
            var added = 0;
            foreach (var name in saudi)
            {
                if (existingSet.Contains(name)) continue;
                db.DiscoveryRegions.Add(new ACommerce.Kits.Discovery.Domain.DiscoveryRegion
                {
                    Name = name, Level = 1, CreatedAt = now,
                });
                added++;
            }
            if (added > 0) await db.SaveChangesAsync(ct);
            logger?.LogInformation("Ashare V3: Saudi cities seed → +{Added} (total {Total})",
                added, existing.Count + added);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Ashare V3: Saudi cities seed skipped");
        }
    }

    private static Task UpsertTaxonomyNodeAsync(
        AshareV3DbContext db, Dictionary<string, TaxonomyNodeEntity> existingByCode,
        Guid id, string rootCode, string code, string name, string nameAr,
        string icon, Guid? parentId, int sortOrder, DateTime now)
    {
        if (existingByCode.TryGetValue(code, out var existing))
        {
            // مَوجود — نُحَدِّث الحُقول الَّتي قَد تَتَغَيَّر بِالـ seed.
            // لا نَلمَس Id (قَد تَكون كَيانات أُخرى تُشير إلَيه).
            var changed = false;
            if (existing.Name      != name)      { existing.Name      = name;      changed = true; }
            if (existing.NameAr    != nameAr)    { existing.NameAr    = nameAr;    changed = true; }
            if (existing.Icon      != icon)      { existing.Icon      = icon;      changed = true; }
            if (existing.SortOrder != sortOrder) { existing.SortOrder = sortOrder; changed = true; }
            if (existing.ParentId  != parentId)  { existing.ParentId  = parentId;  changed = true; }
            if (!existing.IsActive)              { existing.IsActive  = true;      changed = true; }
            if (existing.IsDeleted)              { existing.IsDeleted = false;     changed = true; }
            if (changed) existing.UpdatedAt = now;
            return Task.CompletedTask;
        }

        db.TaxonomyNodes.Add(new TaxonomyNodeEntity
        {
            Id = id, CreatedAt = now,
            RootCode = rootCode, ParentId = parentId,
            Code = code, Name = name, NameAr = nameAr, Icon = icon,
            SortOrder = sortOrder, IsActive = true,
        });
        return Task.CompletedTask;
    }
}
