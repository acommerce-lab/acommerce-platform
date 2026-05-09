using ACommerce.Client.Http;
using ACommerce.ClientHost.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Listings.Frontend.Customer.Stores;

public static class ListingsRoutesExtensions
{
    public static IServiceCollection AddListingsRoutes(this IServiceCollection services)
    {
        services.AddSingleton<IRoutesRegistrar, ListingsRoutesRegistrar>();
        return services;
    }
}

internal sealed class ListingsRoutesRegistrar : IRoutesRegistrar
{
    public void Register(HttpRouteRegistry routes)
    {
        routes.Map("listings.search",    HttpMethod.Get, "/listings");
        routes.Map("listings.get",       HttpMethod.Get, "/listings/{id}");
        routes.Map("listings.list_mine", HttpMethod.Get, "/my-listings");
    }
}
