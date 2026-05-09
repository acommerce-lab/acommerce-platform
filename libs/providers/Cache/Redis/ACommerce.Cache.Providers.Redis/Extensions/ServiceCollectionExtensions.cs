using ACommerce.Cache.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace ACommerce.Cache.Providers.Redis.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل <see cref="ICache"/> كـ Singleton مدعوماً بـ Redis. الـ
    /// <see cref="IConnectionMultiplexer"/> يُسجَّل كـ Singleton وتشاركه
    /// مكتبات أخرى (مثل SignalR backplane) للحفاظ على connection pool واحد.
    ///
    /// <para><b>تذكير</b>: حدّث <c>Cache:Redis:ConnectionString</c> في
    /// <c>appsettings.{Environment}.json</c> لكلّ خدمة خلفيّة قبل النشر.</para>
    /// </summary>
    public static IServiceCollection AddRedisCache(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Redis connection string is required", nameof(connectionString));

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(connectionString));
        services.AddSingleton<ICache, RedisCache>();
        return services;
    }

    /// <summary>
    /// نسخة overload تستعمل <see cref="IConnectionMultiplexer"/> مسجَّلاً مسبقاً
    /// (مثلاً عبر <c>AddSignalRRedisBackplane</c>) — تتجنّب إنشاء اتصالين مختلفين.
    /// </summary>
    public static IServiceCollection AddRedisCache(this IServiceCollection services)
    {
        services.AddSingleton<ICache, RedisCache>();
        return services;
    }
}
