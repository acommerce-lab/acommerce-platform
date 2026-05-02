using ACommerce.SharedKernel.Domain.Entities;
using ACommerce.SharedKernel.Infrastructure.EFCore;
using ACommerce.SharedKernel.Infrastructure.EFCore.Factories;
using ACommerce.SharedKernel.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ACommerce.ServiceHost;

/// <summary>
/// امتدادات الـ ServiceHost عاليّة المستوى — تُحوِّل خمسة سطور من
/// <c>builder.Services.AddXxx</c> إلى دالّة واحدة فصيحة.
/// </summary>
public static class ServiceHostFluentExtensions
{
    /// <summary>
    /// يَفتح كتلة تسجيل الكيتس. كلّ كيت يُسجَّل بسطر:
    /// <code>kits.AddListings&lt;EjarListingStore&gt;()</code>
    /// </summary>
    public static ServiceHostBuilder AddKits(
        this ServiceHostBuilder host, Action<KitBuilder> configure)
    {
        var kits = new KitBuilder(host.Builder.Services, host.Builder.Configuration);
        configure(kits);
        return host;
    }

    /// <summary>
    /// يَفتح كتلة تسجيل التراكيب.
    /// <code>compositions.Add&lt;MarketplaceComposition&gt;()</code>
    /// </summary>
    public static ServiceHostBuilder AddCompositions(
        this ServiceHostBuilder host, Action<CompositionBuilder> configure)
    {
        var c = new CompositionBuilder(host.Builder.Services);
        configure(c);
        return host;
    }

    /// <summary>
    /// يُسجِّل الـ glue plumbing لـ EF: <c>DbContext</c> العام،
    /// <c>IUnitOfWork</c> (لـ <c>SaveAtEnd</c>) و <c>IRepositoryFactory</c>.
    ///
    /// <para><b>التطبيق</b> يَستدعي <c>AddDbContext&lt;TDbContext&gt;</c> أوّلاً
    /// (مع provider switching الخاصّ به) — هذا الـ extension يُكمل البقيّة.</para>
    /// </summary>
    public static ServiceHostBuilder UseDatabase<TDbContext>(this ServiceHostBuilder host)
        where TDbContext : DbContext
    {
        host.Builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());
        host.Builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        host.Builder.Services.AddScoped<IRepositoryFactory, RepositoryFactory>();
        return host;
    }

    /// <summary>
    /// يَمسح الـ assembly المُعطى عن كلّ <c>IBaseEntity</c> ويُسجِّله في
    /// <c>EntityDiscoveryRegistry</c> — يُستهلكه <c>CrudActionInterceptor</c>
    /// في مسار generic CRUD path.
    /// </summary>
    public static ServiceHostBuilder RegisterEntities(
        this ServiceHostBuilder host, params Assembly[] assemblies)
    {
        foreach (var asm in assemblies)
        {
            var entityTypes = asm.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IBaseEntity).IsAssignableFrom(t));
            foreach (var t in entityTypes)
                EntityDiscoveryRegistry.RegisterEntity(t);
        }
        return host;
    }

    /// <summary>
    /// JWT scheme من <c>appsettings.json</c> section. مثال:
    /// <code>jwt.UseJwtAuthenticationFromSection("JWT")</code>
    /// </summary>
    public static ServiceHostBuilder UseJwtAuthenticationFromSection(
        this ServiceHostBuilder host, string sectionName = "JWT")
    {
        var section = host.Builder.Configuration.GetSection(sectionName);
        return host.UseJwtAuthentication(jwt =>
        {
            jwt.Secret   = section["SecretKey"] ?? throw new InvalidOperationException($"{sectionName}:SecretKey is required");
            jwt.Issuer   = section["Issuer"]   ?? "";
            jwt.Audience = section["Audience"] ?? "";
        });
    }
}
