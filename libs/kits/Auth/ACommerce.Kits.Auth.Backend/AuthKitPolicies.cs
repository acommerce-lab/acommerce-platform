using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Auth.Backend;

/// <summary>
/// سياسات Auth kit. <see cref="Authenticated"/> = أيّ مستخدم موثَّق
/// (تستهلكها أيّ نقطة بحاجة فقط للتأكّد من وجود توكن صالح).
/// </summary>
public static class AuthKitPolicies
{
    public const string Authenticated = "Auth.Authenticated";

    public static IServiceCollection AddAuthKitPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(opts =>
            opts.AddPolicy(Authenticated, p => p.RequireAuthenticatedUser()));
        return services;
    }
}
