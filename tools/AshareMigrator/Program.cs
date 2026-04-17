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

        if (string.IsNullOrWhiteSpace(srcConn) || string.IsNullOrWhiteSpace(dstConn))
        {
            Console.Error.WriteLine("""
                ✖ مطلوب سلسلتا اتصال:
                  Source = SQL Server القديم (عشير الإنتاجي)
                  Target = SQLite المحلي الجديد
                أضفهما في appsettings.json أو عبر متغير بيئة ASHARE_MIGRATOR_Source / ASHARE_MIGRATOR_Target،
                أو كوسائط: --src "..." --dst "..."
                """);
            return 1;
        }

        // أزل إعدادات الـ Pool من سلسلة مصدر — أداة ترحيل تعمل باتصال واحد فقط
        var srcConnClean = StripPoolSettings(srcConn);

        Console.WriteLine($"📡 المصدر : (SQL Server) {MaskConn(srcConnClean)}");
        Console.WriteLine($"💾 الهدف  : (SQLite)     {dstConn}");

        var srcOptions = new DbContextOptionsBuilder<LegacyDbContext>()
            .UseSqlServer(srcConnClean, o => o.CommandTimeout(120))
            .Options;

        EnsureDirectoryForSqlite(dstConn);

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
        var legacy = await src.ProductCategories.AsNoTracking().ToListAsync();
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

        var users = await src.Users.AsNoTracking().ToListAsync();
        var profiles = await src.Profiles.AsNoTracking().ToListAsync();
        var vendors = await src.Vendors.AsNoTracking().ToListAsync();

        var profileByUser = profiles.ToDictionary(p => p.UserId);
        var profileToVendor = vendors.ToDictionary(v => v.ProfileId, v => v.Id);
        var userHasVendor = users.ToDictionary(
            u => u.Id,
            u => profileByUser.TryGetValue(u.Id, out var p) && profileToVendor.ContainsKey(p.Id));

        var existingUserIds = await dst.Users.Select(u => u.Id).ToHashSetAsync();
        var newUsers = new List<NewUser>();
        var userMap = users.ToDictionary(u => u.Id, u => u.Id);

        foreach (var u in users)
        {
            if (existingUserIds.Contains(u.Id)) continue;
            profileByUser.TryGetValue(u.Id, out var profile);
            newUsers.Add(UserMapper.Map(u, profile, userHasVendor[u.Id]));
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

        // vendorId → userId للعروض والاشتراكات (كلاهما مرتبط بالبائع في المصدر)
        var vendorToUser = new Dictionary<Guid, Guid>();
        foreach (var v in vendors)
        {
            var profile = profiles.FirstOrDefault(p => p.Id == v.ProfileId);
            if (profile != null) vendorToUser[v.Id] = profile.UserId;
        }

        Console.WriteLine($"{users.Count} مستخدم، {newUsers.Count} مضاف، {newProfiles.Count} ملف شخصي مضاف.");
        return (userMap, vendorToUser);
    }

    private static async Task MigrateListingsAsync(
        LegacyDbContext src,
        TargetDbContext dst,
        Dictionary<Guid, Guid> vendorToUserMap)
    {
        Console.Write("📜 العروض... ");

        var listings = await src.ProductListings.AsNoTracking().ToListAsync();
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
