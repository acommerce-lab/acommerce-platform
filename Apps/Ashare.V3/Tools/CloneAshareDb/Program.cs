using Ashare.V3.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

// ════════════════════════════════════════════════════════════════════════
// CloneAshareDb — يَستَنسِخ asharedb الإنتاجِيَّة (SQL Server) إلى ملف
// SQLite مَحَلّي. يَجِب أَن يُشَغَّل مَرَّة (أَو كُلَّما أَرَدتَ refresh)
// قَبل التَّجارُب — يَسمَح بِالاختِبار عَلى نُسخَة آمِنَة بِلا خَطَر
// كِتابَة عَلى الإنتاج.
//
// الاستِخدام:
//   ASHAREDB_PROD_CONN='Server=…;Database=asharedb;User Id=…;Password=…;TrustServerCertificate=True' \
//   dotnet run --project Apps/Ashare.V3/Tools/CloneAshareDb
//
// أَو مَرَّر الـ ConnectionString كَوَسيط:
//   dotnet run --project Apps/Ashare.V3/Tools/CloneAshareDb -- "Server=…;Database=asharedb;…"
//
// الهَدَف الافتِراضِي:
//   Apps/Ashare.V3/Customer/Backend/Ashare.V3.Api/Data/asharev3.dev.db
// لِتَغييره مَرَّر مَسار ثاني:
//   dotnet run --project … -- "<prod-cs>" "Data Source=/custom/path.db"
// ════════════════════════════════════════════════════════════════════════

var prodCs = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? args[0]
    : Environment.GetEnvironmentVariable("ASHAREDB_PROD_CONN")
      ?? throw new InvalidOperationException(
          "ConnectionString لِلإنتاج غَير مُحَدَّد. مَرِّره كَوَسيط أَوَّل أَو في env var ASHAREDB_PROD_CONN.");

// نَكتُب إلى نَفس المَوقِع المُطلَق الَّذي يَفتَحه V3 API. كِلاهُما يَحُلّ
// "Data/asharev3.dev.db" ضِدّ ContentRoot لِـ V3.Api project. نَستَخرِج هذا
// المَوقِع مَن مَوقِع .csproj لِأَداة الاستِنساخ (Apps/Ashare.V3/Tools/CloneAshareDb)
// ⇒ V3.Api ContentRoot = ../../Customer/Backend/Ashare.V3.Api.
var toolDir   = AppContext.BaseDirectory;                            // …/Tools/CloneAshareDb/bin/Debug/net10.0
var repoRoot  = FindRepoRoot(toolDir) ?? Directory.GetCurrentDirectory();
var apiRoot   = Path.Combine(repoRoot, "Apps", "Ashare.V3", "Customer", "Backend", "Ashare.V3.Api");
var defaultTarget = Path.Combine(apiRoot, "Data", "asharev3.dev.db");

var targetCs = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
    ? args[1]
    : $"Data Source={defaultTarget};Cache=Shared";

var sqliteBuilder = new SqliteConnectionStringBuilder(targetCs);
var dbPath = Path.GetFullPath(sqliteBuilder.DataSource);
sqliteBuilder.DataSource = dbPath;
targetCs = sqliteBuilder.ToString();

var dbDir = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
    Directory.CreateDirectory(dbDir);

if (File.Exists(dbPath))
{
    Console.WriteLine($"حَذف القاعِدَة المَحَلِّيَّة القائِمَة: {dbPath}");
    File.Delete(dbPath);
}

Console.WriteLine($"Source: {Truncate(prodCs, 60)}…");
Console.WriteLine($"Target: {targetCs}");

var sourceOpts = new DbContextOptionsBuilder<AshareV3DbContext>().UseSqlServer(prodCs).Options;
var targetOpts = new DbContextOptionsBuilder<AshareV3DbContext>().UseSqlite(targetCs).Options;

await using var source = new AshareV3DbContext(sourceOpts);
await using var target = new AshareV3DbContext(targetOpts);

Console.WriteLine("بِناء schema الـ SQLite (EnsureCreated)…");
await target.Database.EnsureCreatedAsync();

target.ChangeTracker.AutoDetectChangesEnabled = false;
await target.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;");

var totalRows = 0L;
var t0 = DateTime.UtcNow;

totalRows += await CopyAsync(source.Profiles,           target.Profiles,           target, "Profile");
totalRows += await CopyAsync(source.ProductCategories,  target.ProductCategories,  target, "ProductCategory");
totalRows += await CopyAsync(source.Products,           target.Products,           target, "Products");
totalRows += await CopyAsync(source.ProductListings,    target.ProductListings,    target, "ProductListing");
totalRows += await CopyAsync(source.Chats,              target.Chats,              target, "Chat");
totalRows += await CopyAsync(source.ChatParticipants,   target.ChatParticipants,   target, "ChatParticipant");
totalRows += await CopyAsync(source.Messages,           target.Messages,           target, "Message");
totalRows += await CopyAsync(source.MessageReads,       target.MessageReads,       target, "MessageRead");
totalRows += await CopyAsync(source.Complaints,         target.Complaints,         target, "Complaint");
totalRows += await CopyAsync(source.ComplaintReplies,   target.ComplaintReplies,   target, "ComplaintReply");
totalRows += await CopyAsync(source.Bookings,           target.Bookings,           target, "Booking");
totalRows += await CopyAsync(source.BookingHistory,     target.BookingHistory,     target, "BookingStatusHistory");
totalRows += await CopyAsync(source.DeviceTokens,       target.DeviceTokens,       target, "DeviceTokens");
totalRows += await CopyAsync(source.AppVersions,        target.AppVersions,        target, "AppVersions");
totalRows += await CopyAsync(source.LegalPages,         target.LegalPages,         target, "LegalPage");

totalRows += await CopyOptionalAsync(source.Favorites,           target.Favorites,           target, "Favorites");
totalRows += await CopyOptionalAsync(source.Reports,             target.Reports,             target, "Reports");
totalRows += await CopyOptionalAsync(source.Notifications,       target.Notifications,       target, "Notifications");
totalRows += await CopyOptionalAsync(source.DiscoveryCategories, target.DiscoveryCategories, target, "DiscoveryCategories");
totalRows += await CopyOptionalAsync(source.DiscoveryRegions,    target.DiscoveryRegions,    target, "DiscoveryRegions");
totalRows += await CopyOptionalAsync(source.DiscoveryAmenities,  target.DiscoveryAmenities,  target, "DiscoveryAmenities");

await target.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");

var dt = DateTime.UtcNow - t0;
Console.WriteLine();
Console.WriteLine($"✓ نُسِخَ {totalRows:N0} صَفّاً في {dt.TotalSeconds:F1}s إلى {dbPath}");
Console.WriteLine($"  حَجم المَلَفّ: {new FileInfo(dbPath).Length / 1024.0 / 1024.0:F2} MB");
Console.WriteLine($"  الآن: شَغِّل V3 API — يَتَّصِل بِـ SQLite تِلقائِيّاً.");

return 0;

static async Task<long> CopyAsync<T>(IQueryable<T> src, DbSet<T> tgt, AshareV3DbContext tgtCtx, string label)
    where T : class
{
    Console.Write($"  {label,-25} … ");
    var rows = await src.AsNoTracking().IgnoreQueryFilters().ToListAsync();
    if (rows.Count == 0) { Console.WriteLine("0"); return 0; }

    const int batchSize = 1000;
    for (var i = 0; i < rows.Count; i += batchSize)
    {
        var batch = rows.Skip(i).Take(batchSize).ToList();
        await tgt.AddRangeAsync(batch);
        await tgtCtx.SaveChangesAsync();
        tgtCtx.ChangeTracker.Clear();
    }
    Console.WriteLine($"{rows.Count}");
    return rows.Count;
}

static async Task<long> CopyOptionalAsync<T>(IQueryable<T> src, DbSet<T> tgt, AshareV3DbContext tgtCtx, string label)
    where T : class
{
    try { return await CopyAsync(src, tgt, tgtCtx, label); }
    catch (Exception ex) when (ex.Message.Contains("Invalid object", StringComparison.OrdinalIgnoreCase)
                            || ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                            || ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"  {label,-25} … (لَيس في الإنتاج، تَخَطّى)");
        return 0;
    }
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
