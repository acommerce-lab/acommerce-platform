using ACommerce.Favorites.Operations.Entities;
using ACommerce.SharedKernel.Abstractions.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Favorites.Operations.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يسجّل FavoriteService ويسجّل كيان Favorite في Auto-Discovery.
    /// </summary>
    public static IServiceCollection AddFavoriteOperations(this IServiceCollection services)
    {
        EntityDiscoveryRegistry.RegisterEntity(typeof(Favorite));
        services.AddScoped<FavoriteService>();
        return services;
    }
}
