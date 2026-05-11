using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ashare.V3.Data;

/// <summary>
/// تَسجيل DbContext لِـ Ashare V3. يَدعَم SQL Server (إنتاجيّ، لِـ asharedb)
/// و Sqlite (تَطوير محلّيّ مَعزول). الاختِيار مِن <c>Database:Provider</c>.
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
}
