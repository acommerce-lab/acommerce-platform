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
            logger?.LogInformation("Ashare V3: SQL Server additive schema check complete");
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
          );"
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
            var existing = await db.AttributeDefinitions.AsNoTracking()
                .Where(d => V3ProfileAttributes.Defaults.Select(x => x.Code).Contains(d.Code))
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
            {
                await db.SaveChangesAsync(ct);
                logger?.LogInformation(
                    "Ashare V3: profile attribute seed → +{Defs} defs, +{Maps} mappings",
                    newDefs.Count, newMaps.Count);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Ashare V3: profile attribute seed skipped");
        }
    }
}
