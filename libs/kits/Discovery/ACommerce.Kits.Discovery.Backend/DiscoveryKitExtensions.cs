using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Discovery.Backend;

public static class DiscoveryKitExtensions
{
    public static IServiceCollection AddDiscoveryKit(this IServiceCollection services)
    {
        services.AddControllers().AddApplicationPart(typeof(DiscoveryController).Assembly);
        return services;
    }
}
