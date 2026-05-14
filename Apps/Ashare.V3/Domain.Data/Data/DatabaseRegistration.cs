using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ashare.V3.Data;

/// <summary>
/// تَسجيل DbContext لِـ Ashare V3. يَدعَم SQL Server (إنتاجيّ، لِـ asharedb)
/// و Sqlite (تَطوير محلّيّ مَعزول). الاختِيار مِن <c>Database:Provider</c>.
///
/// <para><b>مَسارات SQLite</b>: أَيّ <c>Data Source=…</c> نِسبي يُحَلّ ضِدّ
/// <c>ContentRootPath</c> (مُجَلَّد المَشروع) لا ضِدّ
/// <c>Directory.GetCurrentDirectory()</c> (الَّذي يَختَلِف بِحَسَب مَن أَطلَق
/// dotnet ومِن أَين). بِدون هذا التَّحويل، شَغّال API مِن جَذر الـ repo
/// يُنشِئ قاعِدَة فارِغَة في مَوقِع آخَر غَير الَّذي كَتَبَت إلَيه أَداة
/// الاستِنساخ ⇒ home فارِغَة.</para>
/// </summary>
public static class DatabaseRegistration
{
    public static IServiceCollection AddAshareV3Database(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment env)
    {
        var provider = config["Database:Provider"]?.ToLowerInvariant() ?? "sqlserver";
        var cs = config["Database:ConnectionString"]
                 ?? config.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException(
                     "Database:ConnectionString غَير مُعَيَّن. حَدِّد قِيمَة في appsettings أَو env var.");

        if (provider is "sqlite")
            cs = ResolveSqlitePath(cs, env.ContentRootPath);

        services.AddDbContext<AshareV3DbContext>(opts =>
        {
            if (provider is "sqlite")
            {
                opts.UseSqlite(cs);
            }
            else
            {
                opts.UseSqlServer(cs, sql =>
                {
                    sql.EnableRetryOnFailure(maxRetryCount: 3,
                                             maxRetryDelay: TimeSpan.FromSeconds(5),
                                             errorNumbersToAdd: null);
                });
            }

            if (env.IsDevelopment())
            {
                opts.EnableSensitiveDataLogging();
                opts.EnableDetailedErrors();
            }
        });

        return services;
    }

    /// <summary>
    /// يُحَوِّل <c>Data Source=Data/asharev3.dev.db</c> النِّسبي إلى مُطلَق
    /// <c>Data Source=&lt;ContentRoot&gt;/Data/asharev3.dev.db</c>. لَو المَسار
    /// مُطلَق أَصلاً يَبقى كَما هو.
    /// </summary>
    public static string ResolveSqlitePath(string connectionString, string contentRoot)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource)) return connectionString;
        if (Path.IsPathRooted(builder.DataSource)) return connectionString;

        // النِّسبي (مَثَلاً "Data/asharev3.dev.db") يُحَلّ ضِدّ ContentRoot.
        builder.DataSource = Path.GetFullPath(Path.Combine(contentRoot, builder.DataSource));

        // تَأَكَّد مِن وُجود المُجَلَّد قَبل ما يَفتَحه أَيّ شَيء.
        var dir = Path.GetDirectoryName(builder.DataSource);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        return builder.ToString();
    }
}
