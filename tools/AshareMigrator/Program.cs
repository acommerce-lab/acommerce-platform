using AshareMigrator.Legacy;
using AshareMigrator.Mappers;
using AshareMigrator.Target;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AshareMigrator;

/// <summary>
/// أداة ترحيل بيانات عشير من قاعدة SQL Server الإنتاجية القديمة إلى قاعدة SQLite محلية بالصيغة الجديدة.
/// التنفيذ: dotnet run --project tools/AshareMigrator -- [--src "..."] [--dst "..."] [--truncate]
/// القراءة فقط من المصدر؛ الكتابة إلى ملف SQLite محلي.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables("ASHARE_MIGRATOR_")
            .AddCommandLine(args, new Dictionary<string, string>
            {
                ["--src"] = "Source",
                ["--dst"] = "Target",
                ["--truncate"] = "Truncate",
            })
            .Build();

        var srcConn = config["Source"] ?? config.GetConnectionString("Source");
        var dstConn = config["Target"] ?? config.GetConnectionString("Target");
        var truncate = string.Equals(config["Truncate"], "true", StringComparison.OrdinalIgnoreCase);
        var discover = args.Contains("--discover");

        if (string.IsNullOrWhiteSpace(srcConn))
        {
            Console.Error.WriteLine("""
                ✖ مطلوب سلسلة اتصال المصدر:
                  أضفها في appsettings.json (مفتاح Source) أو عبر ASHARE_MIGRATOR_Source أو --src "..."
                """);
            return 1;
        }

        if (!discover && string.IsNullOrWhiteSpace(dstConn))
        {
            Console.Error.WriteLine("✖ مطلوب سلسلة اتصال الهدف (Target) لوضع الترحيل.");
            return 1;
        }

        // أزل إعدادات الـ Pool من سلسلة مصدر — أداة ترحيل تعمل باتصال واحد فقط
        var srcConnClean = StripPoolSettings(srcConn);

        Console.WriteLine($"📡 المصدر : (SQL Server) {MaskConn(srcConnClean)}");
        Console.WriteLine($"💾 الهدف  : (SQLite)     {dstConn}");

        var srcOptions = new DbContextOptionsBuilder<LegacyDbContext>()
            .UseSqlServer(srcConnClean, o => o.CommandTimeout(120))
            .Options;

        EnsureDirectoryForSqlite(dstConn!);

        var dstOptions = new DbContextOptionsBuilder<TargetDbContext>()
            .UseSqlite(dstConn)
            .Options;

        await using var src = new LegacyDbContext(srcOptions);
        await using var dst = new TargetDbContext(dstOptions);

        Console.Write("🔌 اختبار الاتصال بـ SQL Server... ");
        try
        {
            await src.Database.OpenConnectionAsync();
            await src.Database.CloseConnectionAsync();
            Console.WriteLine("نجح.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n✖ تعذّر الاتصال بـ SQL Server:\n  {ex.Message}");
            Console.Error.WriteLine("  تحقق من عنوان الخادم، بيانات الاعتماد، وإمكانية الوصول الشبكي.");
            return 3;
        }

        if (discover)
        {
            await DiscoverTablesAsync(src);
            return 0;
        }

        Console.WriteLine("🔧 التأكد من إنشاء قاعدة البيانات الهدف...");
        await dst.Database.EnsureCreatedAsync();

        if (truncate)
        {
            Console.WriteLine("🗑 مسح البيانات الحالية من الهدف...");
            await TruncateAsync(dst);
        }

        try
        {
            await MigrateCategoriesAsync(src, dst);
            var (userMap, vendorUserMap) = await MigrateUsersAndProfilesAsync(src, dst);
            await MigrateListingsAsync(src, dst, vendorUserMap);
            await MigrateBookingsAsync(src, dst, userMap);
            await MigratePlansAsync(src, dst);
            await MigrateSubscriptionsAsync(src, dst, vendorUserMap);

            Console.WriteLine("\n✅ اكتمل الترحيل بنجاح.");
            return 0;
        }
        catch (Exception ex) when (ex.Message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"\n✖ جدول غير موجود في قاعدة المصدر: {ex.Message.Split('\n')[0]}");
            Console.Error.WriteLine("\nالجداول المتاحة في قاعدة المصدر الفعلية:\n");
            await DiscoverTablesAsync(src);
            Console.Error.WriteLine("\nحدّث LegacyDbContext.OnModelCreating لتطابق الأسماء الصحيحة ثم أعد التشغيل.");
            return 2;
        }
        catch (Exception ex) when (ex.Message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"\n✖ عمود غير موجود:\n{ex.Message.Split('\n')[0]}");
            Console.Error.WriteLine("\nأعمدة الجداول المعيّنة في قاعدة المصدر:\n");
            await DiscoverTableColumnsAsync(src,
                "Profile", "Vendor", "ProductCategory",
                "ProductListing", "Booking", "SubscriptionPlans", "Subscriptions");
            Console.Error.WriteLine("\nحدّث LegacyEntities.cs بالأسماء الصحيحة ثم أعد التشغيل.");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n✖ فشل الترحيل: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 2;
        }
    }

    // ─── Steps ───

    private static async Task MigrateCategoriesAsync(LegacyDbContext src, TargetDbContext dst)
    {
        Console.Write("📁 الفئات... ");
        var legacy = await src.Categories.AsNoTracking().ToListAsync();
        var existing = await dst.Categories.Select(c => c.Id).ToHashSetAsync();
        var toAdd = legacy
            .Where(c => !existing.Contains(c.Id))
            .Select(CategoryMapper.Map)
            .ToList();
        if (toAdd.Count > 0) await dst.Categories.AddRangeAsync(toAdd);
        await dst.SaveChangesAsync();
        Console.WriteLine($"{legacy.Count} مقروء، {toAdd.Count} مضاف.");
    }

    private static async Task<(Dictionary<Guid, Guid> userMap, Dictionary<Guid, Guid> vendorToUserMap)>
        MigrateUsersAndProfilesAsync(LegacyDbContext src, TargetDbContext dst)
    {
        Console.Write("👤 المستخدمون + الملفات الشخصية... ");

        // لا يوجد جدول Users في المصدر — نبني المستخدمين من Profile.UserId
        var profiles = await src.Profiles.AsNoTracking().ToListAsync();
        var vendors = await src.Vendors.AsNoTracking().ToListAsync();
        var vendorProfileIds = vendors.Select(v => v.ProfileId).ToHashSet();

        // userId → userId (identity map — نحتاجه لاحقاً للحجوزات)
        var userMap = profiles.ToDictionary(p => p.UserId, p => p.UserId);

        var existingUserIds = await dst.Users.Select(u => u.Id).ToHashSetAsync();
        var newUsers = new List<NewUser>();
        foreach (var p in profiles)
        {
            if (existingUserIds.Contains(p.UserId)) continue;
            var isOwner = vendorProfileIds.Contains(p.Id);
            newUsers.Add(UserMapper.MapFromProfile(p, isOwner));
        }
        if (newUsers.Count > 0) await dst.Users.AddRangeAsync(newUsers);

        var existingProfileIds = await dst.Profiles.Select(p => p.Id).ToHashSetAsync();
        var newProfiles = profiles
            .Where(p => !existingProfileIds.Contains(p.Id))
            .Select(UserMapper.MapProfile)
            .Where(p => p != null)
            .Cast<NewProfile>()
            .ToList();
        if (newProfiles.Count > 0) await dst.Profiles.AddRangeAsync(newProfiles);

        await dst.SaveChangesAsync();

        // vendorId → userId للعروض والاشتراكات
        var vendorToUser = new Dictionary<Guid, Guid>();
        foreach (var v in vendors)
        {
            var profile = profiles.FirstOrDefault(p => p.Id == v.ProfileId);
            if (profile != null) vendorToUser[v.Id] = profile.UserId;
        }

        Console.WriteLine($"{profiles.Count} ملف شخصي، {newUsers.Count} مستخدم مضاف، {newProfiles.Count} ملف مضاف.");
        return (userMap, vendorToUser);
    }

    private static async Task MigrateListingsAsync(
        LegacyDbContext src,
        TargetDbContext dst,
        Dictionary<Guid, Guid> vendorToUserMap)
    {
        Console.Write("📜 العروض... ");

        var listings = await src.Listings.AsNoTracking().ToListAsync();
        var categories = await dst.Categories.AsNoTracking().ToListAsync();
        var templateByCategory = categories.ToDictionary(
            c => c.Id,
            c => ACommerce.SharedKernel.Abstractions.DynamicAttributes.DynamicAttributeHelper.ParseTemplate(c.AttributeTemplateJson));
        var defaultCategoryId = categories.FirstOrDefault()?.Id ?? Guid.Empty;

        var existing = await dst.Listings.Select(l => l.Id).ToHashSetAsync();
        var toAdd = new List<NewListing>();
        var skipped = 0;

        foreach (var l in listings)
        {
            if (existing.Contains(l.Id)) continue;

            if (!vendorToUserMap.TryGetValue(l.VendorId, out var ownerUserId))
            {
                skipped++;
                continue; // بائع بلا ربط بمستخدم — نتخطى ولا نُسقط
            }

            var catId = l.CategoryId ?? defaultCategoryId;
            templateByCategory.TryGetValue(catId, out var template);

            toAdd.Add(ListingMapper.Map(l, ownerUserId, catId, template));
        }
        if (toAdd.Count > 0) await dst.Listings.AddRangeAsync(toAdd);
        await dst.SaveChangesAsync();
        Console.WriteLine($"{listings.Count} مقروء، {toAdd.Count} مضاف، {skipped} متخطى (بائع بلا مستخدم).");
    }

    private static async Task MigrateBookingsAsync(
        LegacyDbContext src,
        TargetDbContext dst,
        Dictionary<Guid, Guid> userMap)
    {
        Console.Write("🗓 الحجوزات... ");
        var bookings = await src.Bookings.AsNoTracking().ToListAsync();
        var existing = await dst.Bookings.Select(b => b.Id).ToHashSetAsync();
        var toAdd = new List<NewBooking>();
        var skipped = 0;

        foreach (var b in bookings)
        {
            if (existing.Contains(b.Id)) continue;
            if (!Guid.TryParse(b.CustomerId, out var customerGuid))
            {
                skipped++;
                continue;
            }
            toAdd.Add(BookingMapper.Map(b, customerGuid));
        }
        if (toAdd.Count > 0) await dst.Bookings.AddRangeAsync(toAdd);
        await dst.SaveChangesAsync();
        Console.WriteLine($"{bookings.Count} مقروء، {toAdd.Count} مضاف، {skipped} متخطى (CustomerId غير صالح).");
    }

    private static async Task MigratePlansAsync(LegacyDbContext src, TargetDbContext dst)
    {
        Console.Write("💳 خطط الاشتراك... ");
        var plans = await src.SubscriptionPlans.AsNoTracking().ToListAsync();
        var existing = await dst.Plans.Select(p => p.Id).ToHashSetAsync();
        var toAdd = plans.Where(p => !existing.Contains(p.Id)).Select(PlanMapper.Map).ToList();
        if (toAdd.Count > 0) await dst.Plans.AddRangeAsync(toAdd);
        await dst.SaveChangesAsync();
        Console.WriteLine($"{plans.Count} مقروء، {toAdd.Count} مضاف.");
    }

    private static async Task MigrateSubscriptionsAsync(
        LegacyDbContext src,
        TargetDbContext dst,
        Dictionary<Guid, Guid> vendorToUserMap)
    {
        Console.Write("🔁 الاشتراكات... ");
        var subs = await src.Subscriptions.AsNoTracking().ToListAsync();
        var existing = await dst.Subscriptions.Select(s => s.Id).ToHashSetAsync();
        var toAdd = new List<NewSubscription>();
        var skipped = 0;

        foreach (var s in subs)
        {
            if (existing.Contains(s.Id)) continue;
            if (!vendorToUserMap.TryGetValue(s.VendorId, out var userId))
            {
                skipped++;
                continue;
            }
            toAdd.Add(SubscriptionMapper.Map(s, userId));
        }
        if (toAdd.Count > 0) await dst.Subscriptions.AddRangeAsync(toAdd);
        await dst.SaveChangesAsync();
        Console.WriteLine($"{subs.Count} مقروء، {toAdd.Count} مضاف، {skipped} متخطى.");
    }

    // ─── Helpers ───

    private static async Task DiscoverTablesAsync(LegacyDbContext src)
    {
        Console.WriteLine("\n📋 الجداول الموجودة في قاعدة المصدر:\n");
        var conn = src.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT TABLE_SCHEMA, TABLE_NAME,
                   (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS c
                    WHERE c.TABLE_SCHEMA = t.TABLE_SCHEMA AND c.TABLE_NAME = t.TABLE_NAME) AS Cols
            FROM INFORMATION_SCHEMA.TABLES t
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            Console.WriteLine($"  [{reader.GetString(0)}].[{reader.GetString(1)}]  ({reader.GetInt32(2)} عمود)");
        await conn.CloseAsync();
        Console.WriteLine("\nاستخدم هذه الأسماء لضبط LegacyDbContext.OnModelCreating إذا اختلفت عن المتوقّع.");
    }

    private static async Task DiscoverTableColumnsAsync(LegacyDbContext src, params string[] tableNames)
    {
        var conn = src.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        foreach (var table in tableNames)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = '{table}'
                ORDER BY ORDINAL_POSITION
                """;
            Console.WriteLine($"── {table} ─────────────────────");
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var len = reader.IsDBNull(3) ? "" : $"({reader.GetValue(3)})";
                Console.WriteLine($"  {reader.GetString(0)}  {reader.GetString(1)}{len}  {reader.GetString(2)}");
            }
            Console.WriteLine();
        }
        await conn.CloseAsync();
    }

    private static async Task TruncateAsync(TargetDbContext dst)
    {
        await dst.Database.ExecuteSqlRawAsync("DELETE FROM Subscriptions;");
        await dst.Database.ExecuteSqlRawAsync("DELETE FROM Plans;");
        await dst.Database.ExecuteSqlRawAsync("DELETE FROM Bookings;");
        await dst.Database.ExecuteSqlRawAsync("DELETE FROM Listings;");
        await dst.Database.ExecuteSqlRawAsync("DELETE FROM Profiles;");
        await dst.Database.ExecuteSqlRawAsync("DELETE FROM Users;");
        await dst.Database.ExecuteSqlRawAsync("DELETE FROM Categories;");
    }

    private static string MaskConn(string conn)
    {
        var parts = conn.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var masked = parts.Select(p =>
        {
            var kv = p.Split('=', 2);
            if (kv.Length != 2) return p;
            var key = kv[0].Trim();
            if (key.Equals("Password", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Pwd", StringComparison.OrdinalIgnoreCase))
                return $"{key}=****";
            return p;
        });
        return string.Join(";", masked);
    }

    // أزل Min/Max Pool Size و Connect Timeout القصير من السلسلة.
    // الأداة لا تحتاج pool — اتصال واحد يكفي، والـ timeout يُحدَّد عبر CommandTimeout.
    private static readonly HashSet<string> PoolKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Min Pool Size", "Max Pool Size", "Pooling", "Connection Lifetime",
        "Connection Reset", "Load Balance Timeout", "Connect Timeout",
    };

    private static string StripPoolSettings(string conn)
    {
        var parts = conn.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var kept = parts.Where(p =>
        {
            var kv = p.Split('=', 2);
            return kv.Length != 2 || !PoolKeys.Contains(kv[0].Trim());
        });
        return string.Join(";", kept);
    }

    private static void EnsureDirectoryForSqlite(string connString)
    {
        var kvs = connString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var kv in kvs)
        {
            var pair = kv.Split('=', 2);
            if (pair.Length != 2) continue;
            if (!pair[0].Trim().Equals("Data Source", StringComparison.OrdinalIgnoreCase)) continue;
            var path = pair[1].Trim();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            return;
        }
    }
}
