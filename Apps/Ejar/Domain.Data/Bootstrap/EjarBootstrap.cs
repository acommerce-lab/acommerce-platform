using ACommerce.Kits.Versions.Backend;
using Ejar.Api.Data;
using Ejar.Api.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ejar.Api.Bootstrap;

/// <summary>
/// نقطة دخول واحدة لتهيئة قاعدة بيانات إيجار عند الإقلاع — تُستدعى من
/// Program.cs بسطر واحد. تُغلِّف:
/// <list type="number">
///   <item>إعادة تعيين القاعدة عند طلب <c>EJAR_DB_RESET=true</c>.</item>
///   <item>SQLite ⇒ <c>EnsureCreated</c>؛ غيره ⇒ <c>Migrate</c> أو
///         <c>EnsureAppVersionsTable</c> لو القاعدة قديمة بـ
///         <c>EnsureCreated()</c> بدون سجلّ migrations.</item>
///   <item>بذور أوّليّة لو الـ Users فاضي + بذور AppVersions.</item>
///   <item>ترقية الـ Latest versions من <c>Versions:Latest:{platform}</c>
///         في appsettings عبر <see cref="VersionsBootstrap"/>.</item>
/// </list>
///
/// <para>الـ Program.cs السابق كان يحوي ~٢٥ سطراً من try/catch + branching
/// لـ provider checking + seeding. الآن السطر الواحد:
/// <c>await EjarBootstrap.MigrateAndSeedAsync(scope.ServiceProvider, builder.Configuration);</c></para>
/// </summary>
public static class EjarBootstrap
{
    /// <summary>
    /// يُهجِّر القاعدة + يُبذَر إن لزم + يُرقّي إصدارات appsettings.
    /// آمن للاستدعاء عند كلّ بدء تشغيل (operations idempotent).
    /// </summary>
    public static async Task MigrateAndSeedAsync(
        IServiceProvider sp,
        IConfiguration   configuration,
        CancellationToken ct = default)
    {
        var db     = sp.GetRequiredService<EjarDbContext>();
        var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Ejar.Bootstrap");

        // ① reset عند طلب صريح
        if (string.Equals(
                Environment.GetEnvironmentVariable("EJAR_DB_RESET"),
                "true", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning("EJAR_DB_RESET=true — dropping database");
            await db.Database.EnsureDeletedAsync(ct);
        }

        // ② SQLite vs SQL Server
        var isSqlite = db.Database.ProviderName?.Contains(
            "Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        if (isSqlite)
        {
            await db.Database.EnsureCreatedAsync(ct);
        }
        else
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count > 0)
            {
                logger?.LogInformation("Applying {N} migration(s): {Names}",
                    pending.Count, string.Join(", ", pending));
                await db.Database.MigrateAsync(ct);
            }
            else
            {
                // قاعدة قديمة بُنيت بـ EnsureCreated() لا تحوي __EFMigrationsHistory
                // — نحتاج التأكّد من جدول AppVersions يدوياً.
                DbInitializer.EnsureAppVersionsTable(db);
            }
        }

        // ③ بذور أوّليّة (idempotent: تتفقّد بنفسها)
        if (!db.Users.Any())
        {
            logger?.LogInformation("Seeding initial data");
            DbInitializer.Seed(db);
        }
        DbInitializer.SeedAppVersionsIfMissing(db);

        // ④ ترقية إصدارات appsettings (غير قاتل لو فشل)
        try
        {
            await VersionsBootstrap.PromoteFromConfigAsync(sp, configuration, ct);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Versions bootstrap failed (non-fatal)");
        }
    }
}
