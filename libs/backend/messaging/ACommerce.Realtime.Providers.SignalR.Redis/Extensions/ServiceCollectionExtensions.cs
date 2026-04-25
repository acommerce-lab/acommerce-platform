using ACommerce.Realtime.Operations.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Realtime.Providers.SignalR.Redis.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يفعّل SignalR Redis backplane: <c>SendToGroupAsync</c>، <c>SendToUserAsync</c>،
    /// و <c>BroadcastAsync</c> تصل لجميع instances المتصلة بنفس Redis تلقائيّاً.
    /// لا يلامس كود الـ apps (لا تغيير في <c>IRealtimeTransport</c>).
    ///
    /// <para><b>تذكير</b>: حدّث <c>Realtime:Redis:ConnectionString</c> في
    /// <c>appsettings.{Environment}.json</c> لكلّ خدمة خلفيّة قبل النشر.</para>
    ///
    /// <para>اخلط مع <c>AddRedisCache(connStr)</c> — كلاهما يفتح
    /// ConnectionMultiplexer منفصلاً (تكلفة TCP اتّصال إضافي صغيرة، اعتمدها
    /// مقابل عزل تشغيليّ).</para>
    /// </summary>
    public static ISignalRServerBuilder AddSignalRRedisBackplane(
        this ISignalRServerBuilder builder, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Redis connection string is required", nameof(connectionString));
        return builder.AddStackExchangeRedis(connectionString);
    }

    /// <summary>
    /// نسخة على <see cref="IServiceCollection"/> للأبسط: تعمل مع <c>AddSignalR()</c> داخليّاً
    /// (آمنة للاستدعاء بعد <c>AddSignalRRealtimeTransport</c>).
    /// </summary>
    public static IServiceCollection AddSignalRRedisBackplane(
        this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Redis connection string is required", nameof(connectionString));
        services.AddSignalR().AddStackExchangeRedis(connectionString);
        return services;
    }

    /// <summary>
    /// يستبدل <see cref="IConnectionTracker"/> بنسخة مدعومة بـ Redis (عبر <c>ICache</c>).</summary>
    public static IServiceCollection AddRedisConnectionTracker(this IServiceCollection services)
    {
        services.AddSingleton<RedisConnectionTracker>();
        services.AddSingleton<IConnectionTracker>(sp => sp.GetRequiredService<RedisConnectionTracker>());
        return services;
    }
}
