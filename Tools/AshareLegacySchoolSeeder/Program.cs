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

// ─── إعدادات ──────────────────────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .AddJsonFile("appsettings.Local.example.json", optional: true) // fallback لِلتَّجرِبَة فَقَط
    .AddEnvironmentVariables()
    .Build();

var apply = string.Equals(Environment.GetEnvironmentVariable("SEED_APPLY"), "true", StringComparison.OrdinalIgnoreCase);

var connectionString = config.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("Server=HOST"))
{
    Console.Error.WriteLine(
        "❌ لا يوجَد ConnectionStrings:DefaultConnection صالِح. انسَخ appsettings.Local.example.json " +
        "إلى appsettings.Local.json واملأ بَيانات الإنتاج.");
    return 1;
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
    await seeder.RunAsync(CancellationToken.None);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"\n❌ فَشَل البَذر: {ex.Message}\n{ex}");
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
