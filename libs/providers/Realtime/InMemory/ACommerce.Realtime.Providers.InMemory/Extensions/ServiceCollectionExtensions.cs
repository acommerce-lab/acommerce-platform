using ACommerce.Realtime.Operations;
using ACommerce.Realtime.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Realtime.Providers.InMemory.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يسجل InMemoryRealtimeTransport وInMemoryConnectionTracker.
    /// مناسب للتطوير والاختبار.
    ///
    /// الاستخدام:
    ///   services.AddInMemoryRealtimeTransport();
    /// </summary>
    public static IServiceCollection AddInMemoryRealtimeTransport(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryRealtimeTransport>();
        services.AddSingleton<IRealtimeTransport>(sp => sp.GetRequiredService<InMemoryRealtimeTransport>());

        services.AddSingleton<InMemoryConnectionTracker>();
        services.AddSingleton<IConnectionTracker>(sp => sp.GetRequiredService<InMemoryConnectionTracker>());

        services.AddRealtimeChannels();

        return services;
    }
}
