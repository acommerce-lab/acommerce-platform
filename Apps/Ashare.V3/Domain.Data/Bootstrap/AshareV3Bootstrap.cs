using ACommerce.Kits.Versions.Backend;
using Ashare.V3.Data;
using Ashare.V3.Api.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ashare.V3.Api.Bootstrap;

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
/// <c>await AshareV3Bootstrap.MigrateAndSeedAsync(scope.ServiceProvider, builder.Configuration);</c></para>
/// </summary>
public static class AshareV3Bootstrap
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
        var db     = sp.GetRequiredService<AshareV3DbContext>();
        var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Ashare.V3.Bootstrap");

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
        // بذور Discovery تَعمل بشكل مُستقلّ — لو DB قديم فيه Users لكن
        // جداول Discovery أُضيفت لاحقاً، الـ Seed الرئيسيّ تَجاوزها (تَفحص
        // Users.Any فقط). هذه تَملؤها حتميّاً.
        DbInitializer.SeedDiscoveryIfMissing(db);
        DbInitializer.SeedAppVersionsIfMissing(db);

        // ترحيل صفوف Favorites القديمة من EntityType="ListingEntity" (الكود
        // قبل Q1) إلى "Listing" (الكود الحاليّ). idempotent.
        DbInitializer.NormalizeFavoriteEntityType(db);

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
