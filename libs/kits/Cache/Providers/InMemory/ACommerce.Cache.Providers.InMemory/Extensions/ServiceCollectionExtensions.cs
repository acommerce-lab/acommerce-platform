using ACommerce.Cache.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Cache.Providers.InMemory.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل <see cref="ICache"/> كـ Singleton مدعوماً بـ <see cref="InMemoryCache"/>
    /// (داخل العمليّة فقط — لا يتقاسم الحالة بين instances).
    /// مناسب للتطوير، وللتطبيقات أحاديّة-العمليّة في الإنتاج.
    /// </summary>
    public static IServiceCollection AddInMemoryCache(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<InMemoryCache>();
        services.AddSingleton<ICache>(sp => sp.GetRequiredService<InMemoryCache>());
        return services;
    }
}
