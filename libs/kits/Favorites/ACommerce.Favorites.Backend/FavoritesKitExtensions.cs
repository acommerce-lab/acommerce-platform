using Microsoft.Extensions.DependencyInjection;
using ACommerce.Favorites.Operations;

namespace ACommerce.Favorites.Backend;

public static class FavoritesKitExtensions
{
    public static IServiceCollection AddFavoritesKit(this IServiceCollection services)
    {
        services.AddScoped<FavoriteService>();
        services.AddControllers().AddApplicationPart(typeof(FavoritesController).Assembly);
        services.AddFavoritesKitPolicies();
        return services;
    }

    /// <summary>تَسجيل Favorites kit مع store يَدعم Mine + Toggle.</summary>
    public static IServiceCollection AddFavoritesKit<TStore>(this IServiceCollection services)
        where TStore : class, IFavoritesStore
    {
        services.AddFavoritesKit();
        services.AddScoped<IFavoritesStore, TStore>();
        return services;
    }
}
