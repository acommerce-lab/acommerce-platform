using ACommerce.Client.Http;
using ACommerce.ClientHost.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Subscriptions.Frontend.Customer.Stores;

public static class SubscriptionsRoutesExtensions
{
    public static IServiceCollection AddSubscriptionsRoutes(this IServiceCollection services)
    {
        services.AddSingleton<IRoutesRegistrar, SubscriptionsRoutesRegistrar>();
        return services;
    }
}

internal sealed class SubscriptionsRoutesRegistrar : IRoutesRegistrar
{
    public void Register(HttpRouteRegistry routes)
    {
        routes.Map("subscriptions.plans.list",  HttpMethod.Get,  "/plans");
        routes.Map("subscription.get_active",   HttpMethod.Get,  "/me/subscription");
        routes.Map("subscription.activate",     HttpMethod.Post, "/subscriptions/activate");
    }
}
