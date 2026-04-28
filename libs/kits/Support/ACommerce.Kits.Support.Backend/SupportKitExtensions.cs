using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Support.Backend;

public static class SupportKitExtensions
{
    public static IServiceCollection AddSupportKit(this IServiceCollection services)
    {
        services.AddControllers().AddApplicationPart(typeof(SupportController).Assembly);
        return services;
    }
}
