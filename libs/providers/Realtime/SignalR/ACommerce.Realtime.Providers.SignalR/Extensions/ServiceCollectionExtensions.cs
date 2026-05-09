using ACommerce.Realtime.Operations;
using ACommerce.Realtime.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Realtime.Providers.SignalR.Extensions;

/// <summary>DI registration for SignalR realtime transport.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSignalRRealtimeTransport(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<IRealtimeTransport, SignalRRealtimeTransport>();
        services.AddRealtimeChannels();
        return services;
    }
}
