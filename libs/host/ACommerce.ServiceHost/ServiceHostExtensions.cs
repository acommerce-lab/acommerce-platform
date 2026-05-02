using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ServiceHost;

public static class ServiceHostExtensions
{
    /// <summary>
    /// نقطة دخول للـ ServiceHost. التطبيق يُمرّر <c>configure</c> الذي
    /// يستدعي modules حسب الحاجة.
    /// </summary>
    /// <example>
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.AddACommerceServiceHost(host =&gt; host
    ///     .UseSerilog("Ejar.Api")
    ///     .UseOperationEngine()
    ///     .UseJwtAuthentication(jwt =&gt;
    ///     {
    ///         jwt.Secret   = builder.Configuration["JWT:SecretKey"]!;
    ///         jwt.Issuer   = builder.Configuration["JWT:Issuer"]!;
    ///         jwt.Audience = builder.Configuration["JWT:Audience"]!;
    ///     })
    ///     .UseRealtime&lt;EjarSignalRTransport, EjarRealtimeHub&gt;()
    ///     .UseCors()
    ///     .UseControllers());
    /// </code>
    /// </example>
    public static WebApplicationBuilder AddACommerceServiceHost(
        this WebApplicationBuilder builder,
        Action<ServiceHostBuilder> configure)
    {
        var host = new ServiceHostBuilder(builder);
        configure(host);

        // نمرّر الـ host كـ singleton (instance موجودة) ليستهلكه
        // UseACommerceServiceHost لاحقاً للوصول لـ AppConfigurators + StartupHooks.
        builder.Services.AddSingleton(host);
        return builder;
    }

    /// <summary>
    /// يطبّق pipeline المعياريّ للـ ServiceHost: middleware modules
    /// المُسجَّلة + ينفّذ StartupHooks. التطبيق يستدعيها مرّة واحدة بعد
    /// <c>builder.Build()</c>، ثمّ يُكمِل بأيّ <c>app.Map*</c> خاصّ به.
    /// </summary>
    public static async Task<WebApplication> UseACommerceServiceHostAsync(this WebApplication app)
    {
        var host = app.Services.GetRequiredService<ServiceHostBuilder>();

        foreach (var configure in host.AppConfigurators)
            configure(app);

        // hooks الإقلاع — Migrate + Seed + version promotion + …
        if (host.StartupHooks.Count > 0)
        {
            using var scope = app.Services.CreateScope();
            foreach (var hook in host.StartupHooks)
                await hook(scope.ServiceProvider);
        }

        return app;
    }

    /// <summary>نسخة sync — لا تستدعي StartupHooks. مفيدة لتطبيقات لا تحتاج hooks.</summary>
    public static WebApplication UseACommerceServiceHost(this WebApplication app)
    {
        var host = app.Services.GetRequiredService<ServiceHostBuilder>();
        foreach (var configure in host.AppConfigurators)
            configure(app);
        return app;
    }
}
