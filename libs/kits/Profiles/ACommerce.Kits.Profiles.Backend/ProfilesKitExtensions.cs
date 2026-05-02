using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Profiles.Backend;

public static class ProfilesKitExtensions
{
    public static IServiceCollection AddProfilesKit<TStore>(this IServiceCollection services)
        where TStore : class, IProfileStore
    {
        services.AddScoped<IProfileStore, TStore>();
        services.AddControllers().AddApplicationPart(typeof(ProfilesController).Assembly);
        return services;
    }
}
