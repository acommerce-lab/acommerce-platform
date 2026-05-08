using ACommerce.Client.Http;
using ACommerce.ClientHost.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Support.Frontend.Customer.Stores;

public static class SupportRoutesExtensions
{
    public static IServiceCollection AddSupportRoutes(this IServiceCollection services)
    {
        services.AddSingleton<IRoutesRegistrar, SupportRoutesRegistrar>();
        return services;
    }
}

internal sealed class SupportRoutesRegistrar : IRoutesRegistrar
{
    public void Register(HttpRouteRegistry routes)
    {
        routes.Map("support.tickets.list",  HttpMethod.Get,  "/support/tickets");
        routes.Map("support.ticket.create", HttpMethod.Post, "/support/tickets");
        routes.Map("support.ticket.reply",  HttpMethod.Post, "/support/tickets/{id}/replies");
    }
}
