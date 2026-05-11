using Ashare.V3.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

// ════════════════════════════════════════════════════════════════════════
// CloneAshareDb — يَستَنسِخ asharedb الإنتاجِيَّة (SQL Server) إلى SQLite
// مَحَلّيّ. streaming + COUNT-first + per-table try/catch.
//
//   ASHAREDB_PROD_CONN='Server=…;…' dotnet run --project Apps/Ashare.V3/Tools/CloneAshareDb
//
// أَو:
//   dotnet run --project Apps/Ashare.V3/Tools/CloneAshareDb -- "Server=…;…"
//
// مُتَغَيِّرات بيئَة اختِيارِيَّة:
//   CLONE_COMMAND_TIMEOUT (ثَوانٍ، افتِراضي 600) — لِجَداوِل ضَخمَة
//   CLONE_BATCH_SIZE      (افتِراضي 500)
// ════════════════════════════════════════════════════════════════════════

var prodCs = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? args[0]
    : Environment.GetEnvironmentVariable("ASHAREDB_PROD_CONN")
      ?? throw new InvalidOperationException(
          "ConnectionString لِلإنتاج غَير مُحَدَّد. مَرِّره كَوَسيط أَوَّل أَو في env var ASHAREDB_PROD_CONN.");

var commandTimeout = int.TryParse(Environment.GetEnvironmentVariable("CLONE_COMMAND_TIMEOUT"), out var ct1)
    ? ct1 : 600;
var batchSize = int.TryParse(Environment.GetEnvironmentVariable("CLONE_BATCH_SIZE"), out var bs)
    ? bs : 500;

var toolDir       = AppContext.BaseDirectory;
var repoRoot      = FindRepoRoot(toolDir) ?? Directory.GetCurrentDirectory();
var apiRoot       = Path.Combine(repoRoot, "Apps", "Ashare.V3", "Customer", "Backend", "Ashare.V3.Api");
var defaultTarget = Path.Combine(apiRoot, "Data", "asharev3.dev.db");

var targetCs = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
    ? args[1]
    : $"Data Source={defaultTarget};Cache=Shared";

var sqliteBuilder = new SqliteConnectionStringBuilder(targetCs);
var dbPath = Path.GetFullPath(sqliteBuilder.DataSource);
sqliteBuilder.DataSource = dbPath;
targetCs = sqliteBuilder.ToString();

var dbDir = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir)) Directory.CreateDirectory(dbDir);

if (File.Exists(dbPath))
{
    Console.WriteLine($"حَذف القاعِدَة المَحَلِّيَّة القائِمَة: {dbPath}");
    File.Delete(dbPath);
}

Console.WriteLine($"Source: {Truncate(prodCs, 60)}…");
Console.WriteLine($"Target: {targetCs}");
Console.WriteLine($"CommandTimeout: {commandTimeout}s | BatchSize: {batchSize}");

var sourceOpts = new DbContextOptionsBuilder<AshareV3DbContext>()
    .UseSqlServer(prodCs, sql =>
    {
        sql.CommandTimeout(commandTimeout);
        sql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
    })
    .Options;

var targetOpts = new DbContextOptionsBuilder<AshareV3DbContext>()
    .UseSqlite(targetCs, sql => sql.CommandTimeout(commandTimeout))
    .Options;

await using var source = new AshareV3DbContext(sourceOpts);
await using var target = new AshareV3DbContext(targetOpts);

Console.Write("اختِبار الاتِّصال بِالإنتاج … ");
try
{
    var canConnect = await source.Database.CanConnectAsync();
    Console.WriteLine(canConnect ? "OK" : "FAILED (CanConnect=false)");
    if (!canConnect) return 2;
}
catch (Exception ex)
{
    Console.WriteLine("FAILED");
    Console.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine($"  لَو الشَبَكَة تَحجِب TCP 1433، استَخدِم VPN.");
    return 2;
}

Console.WriteLine("بِناء schema الـ SQLite (EnsureCreated)…");
await target.Database.EnsureCreatedAsync();

target.ChangeTracker.AutoDetectChangesEnabled = false;
target.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
await target.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;");
await target.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
await target.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");

var totalRows = 0L;
var t0 = DateTime.UtcNow;

totalRows += await CopySafe(source.Profiles,           target.Profiles,           target, "Profile");
totalRows += await CopySafe(source.ProductCategories,  target.ProductCategories,  target, "ProductCategory");
totalRows += await CopySafe(source.Products,           target.Products,           target, "Products");
totalRows += await CopySafe(source.ProductListings,    target.ProductListings,    target, "ProductListing");
totalRows += await CopySafe(source.Chats,              target.Chats,              target, "Chat");
totalRows += await CopySafe(source.ChatParticipants,   target.ChatParticipants,   target, "ChatParticipant");
totalRows += await CopySafe(source.Messages,           target.Messages,           target, "Message");
totalRows += await CopySafe(source.MessageReads,       target.MessageReads,       target, "MessageRead");
totalRows += await CopySafe(source.Complaints,         target.Complaints,         target, "Complaint");
totalRows += await CopySafe(source.ComplaintReplies,   target.ComplaintReplies,   target, "ComplaintReply");
totalRows += await CopySafe(source.Bookings,           target.Bookings,           target, "Booking");
totalRows += await CopySafe(source.BookingHistory,     target.BookingHistory,     target, "BookingStatusHistory");
totalRows += await CopySafe(source.DeviceTokens,       target.DeviceTokens,       target, "DeviceTokens");
totalRows += await CopySafe(source.AppVersions,        target.AppVersions,        target, "AppVersions");
totalRows += await CopySafe(source.LegalPages,         target.LegalPages,         target, "LegalPage");
totalRows += await CopySafe(source.Favorites,          target.Favorites,          target, "Favorites");
totalRows += await CopySafe(source.Reports,            target.Reports,            target, "Reports");
totalRows += await CopySafe(source.Notifications,      target.Notifications,      target, "Notifications");
totalRows += await CopySafe(source.DiscoveryCategories,target.DiscoveryCategories,target, "DiscoveryCategories");
totalRows += await CopySafe(source.DiscoveryRegions,   target.DiscoveryRegions,   target, "DiscoveryRegions");
totalRows += await CopySafe(source.DiscoveryAmenities, target.DiscoveryAmenities, target, "DiscoveryAmenities");

await target.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");

var dt = DateTime.UtcNow - t0;
Console.WriteLine();
Console.WriteLine($"✓ نُسِخَ {totalRows:N0} صَفّاً في {dt.TotalSeconds:F1}s إلى {dbPath}");
Console.WriteLine($"  حَجم المَلَفّ: {new FileInfo(dbPath).Length / 1024.0 / 1024.0:F2} MB");
Console.WriteLine($"  الآن: شَغِّل V3 API — يَتَّصِل بِـ SQLite تِلقائِيّاً.");

return 0;

// ───────────────────────────────────────────────────────────────────────
async Task<long> CopySafe<T>(IQueryable<T> src, DbSet<T> tgt, AshareV3DbContext tgtCtx, string label)
    where T : class
{
    Console.Write($"  {label,-22} ");
    try
    {
        return await CopyStreaming(src, tgt, tgtCtx, label);
    }
    catch (Exception ex) when (ex.Message.Contains("Invalid object", StringComparison.OrdinalIgnoreCase)
                            || ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                            || ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("(لَيس في الإنتاج، تَخَطّى)");
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAILED — {ex.GetType().Name}: {Truncate(ex.Message, 80)}");
        Console.WriteLine($"    تَكمِلَة الجَداوِل التالِيَة…");
        tgtCtx.ChangeTracker.Clear();
        return 0;
    }
}

async Task<long> CopyStreaming<T>(IQueryable<T> src, DbSet<T> tgt, AshareV3DbContext tgtCtx, string label)
    where T : class
{
    // أَوَّلاً اطبَع COUNT(*) لِيَرَى المُستَخدِم حَجم العَمَل.
    Console.Write("counting… ");
    var total = await src.AsNoTracking().IgnoreQueryFilters().CountAsync();
    if (total == 0) { Console.WriteLine("0"); return 0; }
    Console.Write($"{total:N0} rows… ");

    // streaming عَبر AsAsyncEnumerable. لا نُحَمِّل كُلّ شَيء إلى الذاكِرَة
    // مَرَّة واحِدَة — نَقرَأ ونَكتُب دُفعَة بِدُفعَة.
    var copied = 0L;
    var batch = new List<T>(batchSize);
    var lastPrint = DateTime.UtcNow;

    await foreach (var row in src.AsNoTracking().IgnoreQueryFilters().AsAsyncEnumerable())
    {
        batch.Add(row);
        if (batch.Count >= batchSize)
        {
            await FlushBatch(batch, tgt, tgtCtx);
            copied += batch.Count;
            batch.Clear();

            if ((DateTime.UtcNow - lastPrint).TotalSeconds > 2)
            {
                Console.Write($"{copied * 100L / total}% ");
                lastPrint = DateTime.UtcNow;
            }
        }
    }
    if (batch.Count > 0)
    {
        await FlushBatch(batch, tgt, tgtCtx);
        copied += batch.Count;
    }
    Console.WriteLine($"done ({copied:N0})");
    return copied;
}

async Task FlushBatch<T>(List<T> batch, DbSet<T> tgt, AshareV3DbContext tgtCtx) where T : class
{
    await tgt.AddRangeAsync(batch);
    await tgtCtx.SaveChangesAsync();
    tgtCtx.ChangeTracker.Clear();
}

static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

static string? FindRepoRoot(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "ACommerce.Platform.sln"))) return dir.FullName;
        dir = dir.Parent;
    }
    return null;
}
