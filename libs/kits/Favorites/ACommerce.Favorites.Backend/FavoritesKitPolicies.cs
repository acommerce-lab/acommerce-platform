using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Favorites.Backend;

public static class FavoritesKitPolicies
{
    public const string Authenticated = "Favorites.Authenticated";

    public static IServiceCollection AddFavoritesKitPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(opts =>
            opts.AddPolicy(Authenticated, p => p.RequireAuthenticatedUser()));
        return services;
    }
}
