using ACommerce.Files.Abstractions.Providers;
using ACommerce.Files.Storage.AliyunOSS.Extensions;
using ACommerce.Files.Storage.GoogleCloud.Extensions;
using ACommerce.Files.Storage.Local.Extensions;
using ACommerce.SharedKernel.Abstractions.Repositories;
using ACommerce.SharedKernel.Infrastructure.EFCore.Factories;
using ACommerce.SharedKernel.Infrastructure.EFCore.Repositories;
using ACommerce.SharedKernel.Infrastructure.EFCores.Context;
using Ashare.Legacy.SchoolSeeder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ⚠️ إجباريّ: حَمِّل كُلّ assemblies الكِيانات قَبل أَوّل وُصول لِلـ DbContext.
// ApplicationDbContext.OnModelCreating يَكتَشِف الكِيانات بِمَسح
// AppDomain.CurrentDomain.GetAssemblies() (الـ ACommerce.* المُحَمَّلَة فَقَط).
// وَ.NET يُحَمِّل الـ assemblies بِكَسَل، فَلَو لَم نَلمِس نَوعاً مِن كُلّ
// assembly كِيانات قَبل بِناء النَموذَج، تُكتَشَف ProductListing فَقَط (الَّتي
// نَلمِسها أوّلاً) وَيَفشَل أَيّ DbSet آخَر (ProductCategory…). لَمسُ نَوع
// واحِد مِن كُلّ assembly يُجبِر تَحميلها فَتَدخُل كُلّها في النَموذَج.
_ = new[]
{
    typeof(ACommerce.Catalog.Listings.Entities.ProductListing).Assembly,
    typeof(ACommerce.Catalog.Products.Entities.ProductCategory).Assembly,
    typeof(ACommerce.Catalog.Attributes.Entities.AttributeDefinition).Assembly,
    typeof(ACommerce.Catalog.Currencies.Entities.Currency).Assembly,
    typeof(ACommerce.Profiles.Entities.Profile).Assembly,
}.Length;

// ─── إعدادات ──────────────────────────────────────────────────────────────
// ملاحظة: لا نُحَمِّل appsettings.Local.example.json وَقت التَّشغيل — قِيَمه
// الفارِغَة/النائِبَة قَد تَدُسّ فَوقَ القِيَم الحَقيقيّة. هُوَ قالِب فَقَط.
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .AddEnvironmentVariables()              // يَدعَم اصطِلاح __ (Files__Storage__…)
    .AddInMemoryCollection(ResolveStorageFromEnv()) // يَدعَم الأَسماء المُسَطَّحَة في الاستضافة
    .Build();

// يَقرَأ مَفاتيح التَّخزين مِن مُتغَيِّرات البيئة المُسَطَّحَة الَّتي تَستَخدِمها
// استضافَة عشير القديم (ALIYUN_ACCESS_KEY_ID/SECRET، وGCS) وَيُسقِطها عَلى
// مَفاتيح الإعداد Files:Storage:* الَّتي يَربِط بِها مُزَوِّد التَّخزين، مَع
// تَعبئة ثَوابِت الإنتاج (Endpoint/Region/Bucket) تِلقائيّاً. هذا يَجعَل
// الكَيس "مَفاتيح في بيئة الاستضافة" يَعمَل بِلا أَيّ ضَبط يَدويّ.
static IEnumerable<KeyValuePair<string, string?>> ResolveStorageFromEnv()
{
    var d = new Dictionary<string, string?>();
    string? Env(string k) { var v = Environment.GetEnvironmentVariable(k); return string.IsNullOrWhiteSpace(v) ? null : v; }

    var aliyunId     = Env("ALIYUN_ACCESS_KEY_ID");
    var aliyunSecret = Env("ALIYUN_ACCESS_KEY_SECRET");
    if (aliyunId is not null && aliyunSecret is not null)
    {
        const string p = "Files:Storage:AliyunOSS:";
        d[p + "AccessKeyId"]     = aliyunId;
        d[p + "AccessKeySecret"] = aliyunSecret;
        d[p + "Endpoint"]        = Env("ALIYUN_OSS_ENDPOINT")  ?? "oss-me-central-1.aliyuncs.com";
        d[p + "Region"]          = Env("ALIYUN_OSS_REGION")    ?? "me-central-1";
        d[p + "BucketName"]      = Env("ALIYUN_OSS_BUCKET")    ?? "ashare-media";
        d[p + "UseHttps"]        = "true";
        d[p + "UseV4Signature"]  = "true";
    }

    // GCS بَديل: ضَبط المَسار عَبر GOOGLE_APPLICATION_CREDENTIALS + bucket.
    var gcsCreds  = Env("GOOGLE_APPLICATION_CREDENTIALS");
    var gcsBucket = Env("GCS_BUCKET");
    if (gcsCreds is not null)
    {
        const string p = "Files:Storage:GoogleCloud:";
        d[p + "CredentialsPath"] = gcsCreds;
        if (gcsBucket is not null) d[p + "BucketName"] = gcsBucket;
    }
    return d;
}

var apply = string.Equals(Environment.GetEnvironmentVariable("SEED_APPLY"), "true", StringComparison.OrdinalIgnoreCase);

// الوَضع: seed (افتراضيّ) | clone | purge-schools | restore-legacy | rebuild
// rebuild = clone ثُمَّ purge-schools ثُمَّ restore-legacy ثُمَّ seed (التَّسَلسُل
// الَّذي طَلَبه المُستخدِم: نَسخ → حَذف مَدارِس → استِعادَة عشير → بَذر جَديد).
var mode = (Environment.GetEnvironmentVariable("SEEDER_MODE") ?? "seed").Trim().ToLowerInvariant();
bool DoClone   = mode is "clone" or "rebuild";
bool DoPurge   = mode is "purge-schools" or "rebuild";
bool DoRestore = mode is "restore-legacy" or "rebuild";
bool DoSeed    = mode is "seed" or "rebuild";
if (!DoClone && !DoPurge && !DoRestore && !DoSeed)
{
    Console.Error.WriteLine($"❌ SEEDER_MODE غير مَعروف: '{mode}'. القِيَم: seed | clone | purge-schools | restore-legacy | rebuild");
    return 1;
}
Console.WriteLine($"الوَضع: {mode} | SEED_APPLY={(apply ? "true" : "false (dry-run)")}");

// DefaultConnection = القاعِدَة الَّتي نَعمَل عَليها (الجَديدَة في سيناريو rebuild).
var connectionString = config.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("Server=HOST"))
{
    Console.Error.WriteLine(
        "❌ لا يوجَد ConnectionStrings:DefaultConnection صالِح. انسَخ appsettings.Local.example.json " +
        "إلى appsettings.Local.json واملأ بَيانات القاعِدَة (الهَدَف).");
    return 1;
}

// ① clone: يَنسَخ المَصدَر (SourceConnection = الإنتاج) إلى الهَدَف
//    (DefaultConnection = الجَديدَة). يَجري قَبل DI لِأَنَّه يُنشِئ المُخَطَّط.
if (DoClone)
{
    var sourceCs = config.GetConnectionString("SourceConnection");
    if (string.IsNullOrWhiteSpace(sourceCs))
    {
        Console.Error.WriteLine("❌ وَضع clone يَتَطَلَّب ConnectionStrings:SourceConnection (الإنتاج، قِراءَة فَقَط).");
        return 1;
    }
    try
    {
        Console.WriteLine("\n=== نَسخ الإنتاج → القاعِدَة الجَديدَة ===");
        await new DbCloner(sourceCs, connectionString, apply).CloneAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\n❌ فَشَل النَّسخ: {ex.Message}\n{ex}");
        return 2;
    }
}

// ─── DI ─────────────────────────────────────────────────────────────────────
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.AddLogging();
services.AddHttpClient();

services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
        maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null)));
services.AddScoped<DbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
services.AddScoped<IRepositoryFactory, RepositoryFactory>();
services.AddScoped(typeof(IBaseAsyncRepository<>), typeof(BaseAsyncRepository<>));

// التَّخزين: نَفس مُزَوِّد الإنتاج إن وُجِدَت إعداداته — وإلّا محليّ (تَجرِبَة).
RegisterStorage(services, config);

await using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var sp = scope.ServiceProvider;

var storage = sp.GetService<IStorageProvider>();
if (apply && storage is null)
    Console.WriteLine("⚠ لا مُزَوِّد تَخزين مُهَيَّأ — سَتُحفَظ الروابِط الأَصليّة لِلصُّوَر كَما هي.");

var seeder = new LegacySeeder(
    sp.GetRequiredService<IRepositoryFactory>(),
    sp.GetRequiredService<ApplicationDbContext>(),
    storage,
    sp.GetRequiredService<IHttpClientFactory>(),
    config,
    apply);

try
{
    var ct = CancellationToken.None;
    if (DoPurge)   await seeder.PurgeSchoolsAsync(ct);    // ② حَذف المَدارِس
    if (DoRestore) await seeder.RestoreLegacyAsync(ct);   // ③ استِعادَة عشير
    if (DoSeed)    await seeder.RunAsync(ct);             // ④ بَذر جَديد
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"\n❌ فَشَل ({mode}): {ex.Message}\n{ex}");
    return 2;
}

// ─── تَسجيل مُزَوِّد التَّخزين بِناءً عَلى المُتاح في الإعدادات ──────────────
static void RegisterStorage(IServiceCollection services, IConfiguration config)
{
    var aliyun = config.GetSection("Files:Storage:AliyunOSS");
    var gcs = config.GetSection("Files:Storage:GoogleCloud");

    if (!string.IsNullOrWhiteSpace(aliyun["AccessKeyId"]) && !string.IsNullOrWhiteSpace(aliyun["BucketName"]))
    {
        services.AddAliyunOSSFileStorage(config);
        Console.WriteLine("التَّخزين: Aliyun OSS");
    }
    else if (!string.IsNullOrWhiteSpace(gcs["BucketName"]) &&
             !string.IsNullOrWhiteSpace(gcs["CredentialsPath"]) &&
             File.Exists(gcs["CredentialsPath"]!))
    {
        services.AddGoogleCloudStorage(config);
        Console.WriteLine("التَّخزين: Google Cloud Storage");
    }
    else
    {
        // محليّ — لِلتَّجرِبَة فَقَط؛ الروابِط لَن تَعمَل في الإنتاج.
        services.AddLocalFileStorage(config);
        Console.WriteLine("التَّخزين: محليّ (تَجرِبَة فَقَط)");
    }
}
