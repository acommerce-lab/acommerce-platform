using ACommerce.Client.Http;
using ACommerce.ClientHost.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Favorites.Frontend.Customer.Stores;

public static class FavoritesRoutesExtensions
{
    public static IServiceCollection AddFavoritesRoutes(this IServiceCollection services)
    {
        services.AddSingleton<IRoutesRegistrar, FavoritesRoutesRegistrar>();
        return services;
    }
}

internal sealed class FavoritesRoutesRegistrar : IRoutesRegistrar
{
    public void Register(HttpRouteRegistry routes)
    {
        routes.Map("favorites.list",  HttpMethod.Get,  "/favorites");
        routes.Map("favorite.toggle", HttpMethod.Post, "/listings/{id}/favorite");
    }
}
