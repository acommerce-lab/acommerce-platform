using Ashare.V3.Data;
using Ashare.V3.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Text.Json;

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

// Profile: نَقل مُخَصَّص. الـ schema الإنتاجِي "عَريض" (٢٠+ عَمود)؛
// V3 ProfileEntity "ضَيِّق" (مَطابِق لِواجِهَة IUserProfile + أَعمِدَة
// سَطحِيَّة لِخِدمَة التَطبيق). الزِيادات تَنتَقِل لِـ AttributesJson.
totalRows += await CopyProfilesAsync(prodCs, target, commandTimeout, batchSize);

totalRows += await CopySafe(source.ProductCategories,  target.ProductCategories,  target, "ProductCategory");
totalRows += await CopySafe(source.Products,           target.Products,           target, "Products");
// ProductListing: نَقل مُخَصَّص — الجَدول الإنتاجِي يَحوي AttributesJson
// عَريض، V3 يَفصِل الحُقول المَطلوبَة لِواجِهَة IListing (TimeUnit,
// BedroomCount, BathroomCount, AreaSqm, Amenities) كَأَعمِدَة + يُبقي
// الباقي في AttributesJson.
totalRows += await CopyProductListingsAsync(prodCs, target, commandTimeout, batchSize);

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
totalRows += await CopySafe(source.Countries,          target.Countries,          target, "Countries");
totalRows += await CopySafe(source.Regions,            target.Regions,            target, "Regions");
totalRows += await CopySafe(source.Cities,             target.Cities,             target, "Cities");
totalRows += await CopySafe(source.Neighborhoods,      target.Neighborhoods,      target, "Neighborhoods");
totalRows += await CopySafe(source.AttributeDefinitions,       target.AttributeDefinitions,       target, "AttributeDefinitions");
totalRows += await CopySafe(source.AttributeValues,            target.AttributeValues,            target, "AttributeValues");
totalRows += await CopySafe(source.CategoryAttributeMappings,  target.CategoryAttributeMappings,  target, "CategoryAttributeMappings");
totalRows += await CopySafe(source.AttributeValueRelationships,target.AttributeValueRelationships,target, "AttributeValueRelationships");
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

// نَقل مُخَصَّص لِـ Profile: قِراءَة ADO.NET مِن SQL Server (schema عَريض)
// + بِناء V3 ProfileEntity ضَيِّق (مَطابِق لِـ IUserProfile + أَعمِدَة
// سَطحِيَّة) + ضَخّ المُتَبَقّي في <c>AttributesJson</c>.
//
// الأَعمِدَة المَنقولَة لِـ AttributesJson: Address, Country, PostalCode,
// Coordinates (تَطابِق <c>V3ProfileAttributes.Defaults</c>). أَيّ زِيادَة
// مُستَقبَلِيَّة في prod تُضاف بِسُهولَة هُنا + في seed Bootstrap.
async Task<long> CopyProfilesAsync(string prodConn, AshareV3DbContext tgtCtx,
                                   int cmdTimeout, int batchSz)
{
    Console.Write($"  {"Profile",-22} ");
    long copied = 0;
    try
    {
        await using var conn = new SqlConnection(prodConn);
        await conn.OpenAsync();

        await using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM [dbo].[Profile]";
            countCmd.CommandTimeout = cmdTimeout;
            var totalObj = await countCmd.ExecuteScalarAsync();
            var total = totalObj is null ? 0 : Convert.ToInt64(totalObj);
            Console.Write($"counting… {total:N0} rows… ");
            if (total == 0) { Console.WriteLine("0"); return 0; }
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = cmdTimeout;
        cmd.CommandText = @"
            SELECT  Id, UserId, NationalId, [Type], FullName, BusinessName,
                    PhoneNumber, Email, Avatar, Address, City, Country,
                    PostalCode, Coordinates, IsActive, IsVerified, VerifiedAt,
                    CreatedAt, UpdatedAt, IsDeleted
            FROM    [dbo].[Profile]";

        await using var rdr = await cmd.ExecuteReaderAsync();
        var batch = new List<ProfileEntity>(batchSz);
        var lastPrint = DateTime.UtcNow;

        while (await rdr.ReadAsync())
        {
            // كُلّ ما لا يَنتَمي لِواجِهَة <c>IUserProfile</c> + الأَعمِدَة
            // السَطحِيَّة (UserId, NationalId) يَنتَقِل لِـ AttributesJson.
            // هذا يُحَقِّق قاعِدَة "العَمود ⇔ الواجِهَة": الـ entity المَحَلِّي
            // يَحوي حُقول الواجِهَة + هَويَّتَين فَقَط؛ الباقي يُقرَأ مَن JSON.
            var address       = rdr["Address"]      as string;
            var country       = rdr["Country"]      as string;
            var postalCode    = rdr["PostalCode"]   as string;
            var coordinates   = rdr["Coordinates"]  as string;
            var businessName  = rdr["BusinessName"] as string;
            var profileType   = rdr["Type"] is int t ? t : 0;
            var isActive      = rdr["IsActive"] is bool ia && ia;
            var isVerified    = rdr["IsVerified"] is bool iv && iv;
            var verifiedAt    = rdr["VerifiedAt"] as DateTime?;

            string? attrsJson = null;
            var attrs = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(businessName)) attrs["BusinessName"] = businessName;
            if (profileType != 0)                          attrs["Type"]         = profileType;
            // IsActive افتِراضي true في الكيان — نُسَجِّل فَقَط الحالة
            // المُختَلِفَة (false = مُعَطَّل).
            if (!isActive)                                 attrs["IsActive"]     = false;
            if (isVerified)                                attrs["IsVerified"]   = true;
            if (verifiedAt.HasValue)                       attrs["VerifiedAt"]   = verifiedAt.Value;
            if (!string.IsNullOrWhiteSpace(address))       attrs["Address"]      = address;
            if (!string.IsNullOrWhiteSpace(country))       attrs["Country"]      = country;
            if (!string.IsNullOrWhiteSpace(postalCode))    attrs["PostalCode"]   = postalCode;
            if (!string.IsNullOrWhiteSpace(coordinates))   attrs["Coordinates"]  = coordinates;
            if (attrs.Count > 0)
                attrsJson = JsonSerializer.Serialize(attrs);

            batch.Add(new ProfileEntity
            {
                Id            = rdr.GetGuid(rdr.GetOrdinal("Id")),
                CreatedAt     = rdr["CreatedAt"] is DateTime ca ? ca : DateTime.UtcNow,
                UpdatedAt     = rdr["UpdatedAt"] as DateTime?,
                IsDeleted     = rdr["IsDeleted"] is bool isd && isd,
                UserId        = rdr["UserId"]   as string,
                NationalId    = rdr["NationalId"] as string,
                FullName      = rdr["FullName"] as string,
                Phone         = rdr["PhoneNumber"] as string,
                Email         = rdr["Email"]   as string,
                City          = rdr["City"]    as string,
                AvatarUrl     = rdr["Avatar"]  as string,
                // PhoneVerified/EmailVerified — لا أَعمِدَة مُقابِلَة في prod؛
                // نَستَخدِم isVerified كَتَقريب لِـ PhoneVerified (Nafath flow).
                PhoneVerified = isVerified,
                EmailVerified = false,
                AttributesJson = attrsJson,
            });

            if (batch.Count >= batchSz)
            {
                await tgtCtx.Profiles.AddRangeAsync(batch);
                await tgtCtx.SaveChangesAsync();
                tgtCtx.ChangeTracker.Clear();
                copied += batch.Count;
                batch.Clear();
                if ((DateTime.UtcNow - lastPrint).TotalSeconds > 2)
                {
                    Console.Write($"{copied:N0} ");
                    lastPrint = DateTime.UtcNow;
                }
            }
        }
        if (batch.Count > 0)
        {
            await tgtCtx.Profiles.AddRangeAsync(batch);
            await tgtCtx.SaveChangesAsync();
            tgtCtx.ChangeTracker.Clear();
            copied += batch.Count;
        }
        Console.WriteLine($"done ({copied:N0})");
        return copied;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAILED — {ex.GetType().Name}: {Truncate(ex.Message, 80)}");
        tgtCtx.ChangeTracker.Clear();
        return 0;
    }
}

// نَقل مُخَصَّص لِـ ProductListing: قِراءَة ADO.NET + رَفع المَفاتيح
// المُطابِقَة لِأَعمِدَة IListing مَن AttributesJson إلى أَعمِدَة + حَذفها
// مَن JSON. هذا يُحَقِّق قاعِدَة "العَمود ⇔ الواجِهَة" بِلا فُقدان بَيانات.
//
// مَفاتيح مَعروفَة + هَدَفها:
//   bedrooms     / bedroom_count / BedroomCount   → BedroomCount
//   bathrooms    / bathroom_count / BathroomCount → BathroomCount
//   area / area_sqm / AreaSqm / size              → AreaSqm
//   time_unit / timeUnit / TimeUnit / unit        → TimeUnit
//   amenities / Amenities (array)                 → AmenitiesJson
async Task<long> CopyProductListingsAsync(string prodConn, AshareV3DbContext tgtCtx,
                                          int cmdTimeout, int batchSz)
{
    Console.Write($"  {"ProductListing",-22} ");
    long copied = 0;
    try
    {
        await using var conn = new SqlConnection(prodConn);
        await conn.OpenAsync();

        await using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM [dbo].[ProductListing]";
            countCmd.CommandTimeout = cmdTimeout;
            var totalObj = await countCmd.ExecuteScalarAsync();
            var total = totalObj is null ? 0 : Convert.ToInt64(totalObj);
            Console.Write($"counting… {total:N0} rows… ");
            if (total == 0) { Console.WriteLine("0"); return 0; }
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = cmdTimeout;
        cmd.CommandText = "SELECT * FROM [dbo].[ProductListing]";

        await using var rdr = await cmd.ExecuteReaderAsync();
        var batch = new List<ProductListingEntity>(batchSz);
        var lastPrint = DateTime.UtcNow;

        // عَدَد الأَعمِدَة مُتَغَيِّر بَين بيئات؛ نَستَخدِم HasColumn.
        bool HasColumn(string name)
        {
            for (var i = 0; i < rdr.FieldCount; i++)
                if (string.Equals(rdr.GetName(i), name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        T? Get<T>(string name) where T : class
        {
            if (!HasColumn(name)) return null;
            var v = rdr[name];
            return v is DBNull ? null : (T)v;
        }
        bool? GetBool(string name)
        {
            if (!HasColumn(name)) return null;
            var v = rdr[name];
            return v is DBNull ? null : Convert.ToBoolean(v);
        }
        int? GetInt(string name)
        {
            if (!HasColumn(name)) return null;
            var v = rdr[name];
            return v is DBNull ? null : Convert.ToInt32(v);
        }
        decimal? GetDec(string name)
        {
            if (!HasColumn(name)) return null;
            var v = rdr[name];
            return v is DBNull ? null : Convert.ToDecimal(v);
        }
        double? GetDouble(string name)
        {
            if (!HasColumn(name)) return null;
            var v = rdr[name];
            return v is DBNull ? null : Convert.ToDouble(v);
        }
        DateTime? GetDate(string name)
        {
            if (!HasColumn(name)) return null;
            var v = rdr[name];
            return v is DBNull ? null : Convert.ToDateTime(v);
        }
        Guid? GetGuid(string name)
        {
            if (!HasColumn(name)) return null;
            var v = rdr[name];
            return v is DBNull ? null : (Guid)v;
        }

        while (await rdr.ReadAsync())
        {
            var attrsJsonRaw = Get<string>("AttributesJson");
            var (promoted, leftover) = PromoteListingAttrs(attrsJsonRaw);
            var (timeUnit, bedroomCount, bathroomCount, areaSqm, amenitiesJson) = promoted;

            batch.Add(new ProductListingEntity
            {
                Id              = GetGuid("Id") ?? Guid.NewGuid(),
                CreatedAt       = GetDate("CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt       = GetDate("UpdatedAt"),
                IsDeleted       = GetBool("IsDeleted") ?? false,
                VendorId        = GetGuid("VendorId") ?? Guid.Empty,
                ProductId       = GetGuid("ProductId") ?? Guid.Empty,
                CategoryId      = GetGuid("CategoryId"),
                Title           = Get<string>("Title") ?? "",
                Description     = Get<string>("Description"),
                VendorSku       = Get<string>("VendorSku"),
                Status          = GetInt("Status") ?? 0,
                Price           = GetDec("Price") ?? 0m,
                CompareAtPrice  = GetDec("CompareAtPrice"),
                Cost            = GetDec("Cost"),
                CurrencyId      = GetGuid("CurrencyId"),
                QuantityAvailable = GetInt("QuantityAvailable") ?? 0,
                QuantityReserved  = GetInt("QuantityReserved")  ?? 0,
                LowStockThreshold = GetInt("LowStockThreshold"),
                ProcessingTime    = GetInt("ProcessingTime"),
                VendorNotes       = Get<string>("VendorNotes"),
                StartsAt          = GetDate("StartsAt"),
                EndsAt            = GetDate("EndsAt"),
                IsActive          = GetBool("IsActive") ?? true,
                IsFeatured        = GetBool("IsFeatured") ?? false,
                IsNew             = GetBool("IsNew") ?? false,
                TotalSales        = GetInt("TotalSales") ?? 0,
                ViewCount         = GetInt("ViewCount") ?? 0,
                Rating            = GetDec("Rating"),
                ReviewCount       = GetInt("ReviewCount") ?? 0,
                ImagesJson        = Get<string>("ImagesJson"),
                FeaturedImage     = Get<string>("FeaturedImage"),
                Latitude          = GetDouble("Latitude"),
                Longitude         = GetDouble("Longitude"),
                Address           = Get<string>("Address"),
                City              = Get<string>("City"),
                Condition         = Get<string>("Condition"),
                Currency          = Get<string>("Currency"),
                CommissionPercentage = GetDec("CommissionPercentage") ?? 0m,
                // الحُقول المَرفوعَة:
                TimeUnit        = timeUnit,
                BedroomCount    = bedroomCount,
                BathroomCount   = bathroomCount,
                AreaSqm         = areaSqm,
                AmenitiesJson   = amenitiesJson,
                // ما تَبَقّى مَن JSON بَعد إزالَة المَفاتيح المَرفوعَة:
                AttributesJson  = leftover,
            });

            if (batch.Count >= batchSz)
            {
                await tgtCtx.ProductListings.AddRangeAsync(batch);
                await tgtCtx.SaveChangesAsync();
                tgtCtx.ChangeTracker.Clear();
                copied += batch.Count;
                batch.Clear();
                if ((DateTime.UtcNow - lastPrint).TotalSeconds > 2)
                {
                    Console.Write($"{copied:N0} ");
                    lastPrint = DateTime.UtcNow;
                }
            }
        }
        if (batch.Count > 0)
        {
            await tgtCtx.ProductListings.AddRangeAsync(batch);
            await tgtCtx.SaveChangesAsync();
            tgtCtx.ChangeTracker.Clear();
            copied += batch.Count;
        }
        Console.WriteLine($"done ({copied:N0})");
        return copied;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAILED — {ex.GetType().Name}: {Truncate(ex.Message, 80)}");
        tgtCtx.ChangeTracker.Clear();
        return 0;
    }
}

((string? TimeUnit, int BedroomCount, int BathroomCount, int AreaSqm, string? AmenitiesJson) Promoted,
 string? Leftover) PromoteListingAttrs(string? raw)
{
    var emptyPromoted = ((string?)null, 0, 0, 0, (string?)null);
    if (string.IsNullOrWhiteSpace(raw)) return (emptyPromoted, null);

    try
    {
        using var doc = JsonDocument.Parse(raw);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return (emptyPromoted, raw);

        string? timeUnit = null;
        int bedrooms = 0, bathrooms = 0, areaSqm = 0;
        string? amenitiesJson = null;
        var leftover = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var k = prop.Name;
            if (MatchKey(k, "time_unit", "timeUnit", "TimeUnit", "unit"))
            {
                timeUnit = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
            }
            else if (MatchKey(k, "bedrooms", "bedroom_count", "bedroomCount", "BedroomCount"))
            {
                bedrooms = TryIntJson(prop.Value);
            }
            else if (MatchKey(k, "bathrooms", "bathroom_count", "bathroomCount", "BathroomCount"))
            {
                bathrooms = TryIntJson(prop.Value);
            }
            else if (MatchKey(k, "area", "area_sqm", "areaSqm", "AreaSqm", "size"))
            {
                areaSqm = TryIntJson(prop.Value);
            }
            else if (MatchKey(k, "amenities", "Amenities"))
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                    amenitiesJson = prop.Value.GetRawText();
            }
            else
            {
                leftover[k] = prop.Value.Clone();
            }
        }

        string? leftoverJson = leftover.Count > 0 ? JsonSerializer.Serialize(leftover) : null;
        return ((timeUnit, bedrooms, bathrooms, areaSqm, amenitiesJson), leftoverJson);
    }
    catch
    {
        return (emptyPromoted, raw);
    }
}

static bool MatchKey(string key, params string[] candidates) =>
    candidates.Any(c => string.Equals(key, c, StringComparison.OrdinalIgnoreCase));

static int TryIntJson(JsonElement el) => el.ValueKind switch
{
    JsonValueKind.Number => el.TryGetInt32(out var i) ? i : (int)el.GetDouble(),
    JsonValueKind.String => int.TryParse(el.GetString(), out var i) ? i : 0,
    _ => 0,
};

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
