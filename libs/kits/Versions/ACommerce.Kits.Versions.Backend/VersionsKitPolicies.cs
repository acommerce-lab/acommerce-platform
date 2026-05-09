using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Versions.Backend;

public static class VersionsKitPolicies
{
    /// <summary>إدارة الإصدارات: upsert، set status، delete. للأدمن فقط افتراضيّاً.</summary>
    public const string Admin = "Versions.Admin";

    public static IServiceCollection AddVersionsKitPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(opts =>
            opts.AddPolicy(Admin, p => p.RequireAuthenticatedUser().RequireRole("admin", "Admin")));
        return services;
    }
}
