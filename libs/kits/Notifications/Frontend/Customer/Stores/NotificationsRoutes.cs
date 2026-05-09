using ACommerce.Client.Http;
using ACommerce.ClientHost.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Notifications.Frontend.Customer.Stores;

public static class NotificationsRoutesExtensions
{
    public static IServiceCollection AddNotificationsRoutes(this IServiceCollection services)
    {
        services.AddSingleton<IRoutesRegistrar, NotificationsRoutesRegistrar>();
        return services;
    }
}

internal sealed class NotificationsRoutesRegistrar : IRoutesRegistrar
{
    public void Register(HttpRouteRegistry routes)
    {
        routes.Map("notifications.list",          HttpMethod.Get,  "/notifications");
        routes.Map("notification.mark_read",      HttpMethod.Post, "/notifications/{id}/read");
        routes.Map("notifications.mark_all_read", HttpMethod.Post, "/notifications/read-all");
    }
}
