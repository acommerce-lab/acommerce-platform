using ACommerce.Client.Http;
using ACommerce.ClientHost.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Reports.Frontend.Customer.Stores;

public static class ReportsRoutesExtensions
{
    public static IServiceCollection AddReportsRoutes(this IServiceCollection services)
    {
        services.AddSingleton<IRoutesRegistrar, ReportsRoutesRegistrar>();
        return services;
    }
}

internal sealed class ReportsRoutesRegistrar : IRoutesRegistrar
{
    public void Register(HttpRouteRegistry routes)
    {
        routes.Map("reports.create", HttpMethod.Post, "/reports");
    }
}
