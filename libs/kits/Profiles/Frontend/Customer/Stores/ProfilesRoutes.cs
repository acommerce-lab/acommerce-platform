using ACommerce.Client.Http;
using ACommerce.ClientHost.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Profiles.Frontend.Customer.Stores;

public static class ProfilesRoutesExtensions
{
    public static IServiceCollection AddProfilesRoutes(this IServiceCollection services)
    {
        services.AddSingleton<IRoutesRegistrar, ProfilesRoutesRegistrar>();
        return services;
    }
}

internal sealed class ProfilesRoutesRegistrar : IRoutesRegistrar
{
    public void Register(HttpRouteRegistry routes)
    {
        routes.Map("profile.get_mine", HttpMethod.Get, "/me/profile");
        routes.Map("profile.update",   HttpMethod.Put, "/me/profile");
    }
}
