using Ashare.V3.Data;
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
    public static async Task EnsureSchemaAsync(
        IServiceProvider sp,
        IConfiguration config,
        CancellationToken ct = default)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AshareV3DbContext>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()
                          ?.CreateLogger("Ashare.V3.Bootstrap");

        // ① اتِّصال
        try
        {
            var ok = await db.Database.CanConnectAsync(ct);
            if (!ok) throw new InvalidOperationException("CanConnectAsync returned false");
            logger?.LogInformation("Ashare V3: connected to DB");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ashare V3: DB connection failed — abort");
            throw;
        }

        // ② التَأَكُّد مِن جَداوِل V3 الجَديدَة فَقَط (additive، آمِن)
        var isSqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        var sql = isSqlite ? SqliteNewTables : SqlServerNewTables;
        foreach (var stmt in sql)
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
        logger?.LogInformation("Ashare V3: schema check complete (new tables ensured)");
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
          CREATE INDEX [IX_Reports_EntityType_EntityId] ON [dbo].[Reports] ([EntityType], [EntityId]);"
    };

    // Sqlite: CREATE TABLE IF NOT EXISTS
    private static readonly string[] SqliteNewTables = new[]
    {
        @"CREATE TABLE IF NOT EXISTS Favorites (
            Id        TEXT NOT NULL PRIMARY KEY,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NULL,
            IsDeleted INTEGER NOT NULL,
            UserId    TEXT NOT NULL,
            ListingId TEXT NOT NULL,
            UNIQUE (UserId, ListingId)
          );",
        @"CREATE TABLE IF NOT EXISTS Reports (
            Id              TEXT NOT NULL PRIMARY KEY,
            CreatedAt       TEXT NOT NULL,
            UpdatedAt       TEXT NULL,
            IsDeleted       INTEGER NOT NULL,
            ReporterId      TEXT NOT NULL,
            EntityType      TEXT NOT NULL,
            EntityId        TEXT NOT NULL,
            Reason          TEXT NOT NULL,
            Description     TEXT NULL,
            Status          TEXT NOT NULL,
            ResolvedAt      TEXT NULL,
            ResolvedById    TEXT NULL,
            ResolutionNotes TEXT NULL
          );",
        @"CREATE TABLE IF NOT EXISTS Notifications (
            Id           TEXT NOT NULL PRIMARY KEY,
            CreatedAt    TEXT NOT NULL,
            UpdatedAt    TEXT NULL,
            IsDeleted    INTEGER NOT NULL,
            UserId       TEXT NOT NULL,
            Title        TEXT NOT NULL,
            Body         TEXT NOT NULL,
            Kind         TEXT NOT NULL,
            IsRead       INTEGER NOT NULL,
            ReadAt       TEXT NULL,
            DeepLinkUrl  TEXT NULL,
            MetadataJson TEXT NULL
          );",
        @"CREATE INDEX IF NOT EXISTS IX_Notifications_UserId_IsRead ON Notifications (UserId, IsRead);",
        @"CREATE INDEX IF NOT EXISTS IX_Reports_EntityType_EntityId ON Reports (EntityType, EntityId);"
    };
}
