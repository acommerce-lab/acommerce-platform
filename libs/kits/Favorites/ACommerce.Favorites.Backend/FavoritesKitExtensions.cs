using Microsoft.Extensions.DependencyInjection;
using ACommerce.Favorites.Operations;

namespace ACommerce.Favorites.Backend;

public static class FavoritesKitExtensions
{
    public static IServiceCollection AddFavoritesKit(this IServiceCollection services)
    {
        services.AddScoped<FavoriteService>();
        services.AddControllers().AddApplicationPart(typeof(FavoritesController).Assembly);
        return services;
    }
}
