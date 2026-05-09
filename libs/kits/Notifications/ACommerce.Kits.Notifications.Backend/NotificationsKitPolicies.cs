using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Notifications.Backend;

public static class NotificationsKitPolicies
{
    public const string Authenticated = "Notifications.Authenticated";

    public static IServiceCollection AddNotificationsKitPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(opts =>
            opts.AddPolicy(Authenticated, p => p.RequireAuthenticatedUser()));
        return services;
    }
}
