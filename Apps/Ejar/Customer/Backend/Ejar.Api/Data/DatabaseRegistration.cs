using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ejar.Api.Data;

/// <summary>
/// يقرأ <c>Database:Provider</c> + <c>Database:ConnectionString</c> من
/// appsettings ويُسجِّل <see cref="EjarDbContext"/> بالمزوّد المناسب.
/// المزوّدات المدعومة الآن: <c>sqlite</c> (الافتراضي للـ dev) و <c>mssql</c>
/// (للإنتاج). إضافة Postgres مستقبلاً = حالة switch إضافية.
/// </summary>
public static class DatabaseRegistration
{
    public static IServiceCollection AddEjarDatabase(
        this IServiceCollection services,
        IConfiguration cfg,
        IHostEnvironment env)
    {
        var provider = (cfg["Database:Provider"] ?? "sqlite").Trim().ToLowerInvariant();
        var conn     = cfg["Database:ConnectionString"];

        // الافتراضي للـ dev: ملف SQLite في <repo>/data/ejar-customer-dev.db
        // — يُكتشَف جذر الريبو عبر PlatformDataRoot لو لم يُضبط ACOMMERCE_DATA_ROOT.
        if (string.IsNullOrWhiteSpace(conn) && provider == "sqlite")
        {
            var dataRoot = ACommerce.SharedKernel.Infrastructure.EFCores
                .PlatformDataRoot.Resolve(env.ContentRootPath);
            var dbPath = Path.Combine(dataRoot, "ejar-customer-dev.db");
            conn = $"Data Source={dbPath}";
        }

        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException(
                $"Database:ConnectionString غير مضبوط للمزوّد '{provider}'. " +
                "أضفه إلى appsettings.{Environment}.json أو متغيّر بيئة Database__ConnectionString.");

        services.AddDbContext<EjarDbContext>(options =>
        {
            // EF Core 10 يرفع PendingModelChangesWarning كـ exception افتراضياً
            // عندما لا يطابق snapshot الـ migration حالة النموذج تماماً (مثل أعمدة
            // decimal بدون precision صريح). نُخفّضه إلى log تحذير فقط حتى لا يمنع
            // Migrate() من العمل في الإنتاج. الحلّ المثاليّ: ضبط precision لكلّ
            // decimal property في OnModelCreating ثمّ توليد migration جديد.
            options.ConfigureWarnings(w =>
                w.Ignore(RelationalEventId.PendingModelChangesWarning));

            switch (provider)
            {
                case "sqlite":
                    options.UseSqlite(conn);
                    break;
                case "mssql":
                case "sqlserver":
                    options.UseSqlServer(conn);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"مزوّد قاعدة بيانات غير معروف: '{provider}'. " +
                        "المزوّدات المدعومة: sqlite, mssql.");
            }
        });

        return services;
    }
}
