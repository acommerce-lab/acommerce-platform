using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Subscriptions.Backend;

public static class SubscriptionsKitPolicies
{
    /// <summary>اشتراك المستخدم الذاتيّ + فواتيره + تفعيل باقة.</summary>
    public const string Authenticated = "Subscriptions.Authenticated";

    public static IServiceCollection AddSubscriptionsKitPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(opts =>
            opts.AddPolicy(Authenticated, p => p.RequireAuthenticatedUser()));
        return services;
    }
}
