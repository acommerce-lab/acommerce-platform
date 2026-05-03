using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Profiles.Backend;

public static class ProfilesKitPolicies
{
    /// <summary>قراءة/كتابة profile الذات. ownership تُفرض في الـ store عبر CallerId.</summary>
    public const string Self = "Profiles.Self";

    public static IServiceCollection AddProfilesKitPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(opts =>
            opts.AddPolicy(Self, p => p.RequireAuthenticatedUser()));
        return services;
    }
}
