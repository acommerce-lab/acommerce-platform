using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.Authentication.TwoFactor.Providers.Nafath.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Authentication.TwoFactor.Providers.Nafath.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يضيف قناة Nafath 2FA.
    ///
    ///   services.AddNafathTwoFactor(configuration);
    /// </summary>
    public static IServiceCollection AddNafathTwoFactor(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = NafathOptions.SectionName)
    {
        var options = configuration.GetSection(sectionName).Get<NafathOptions>() ?? new NafathOptions();
        return services.AddNafathTwoFactor(options);
    }

    /// <summary>يضيف قناة Nafath مع خيارات مخصصة</summary>
    public static IServiceCollection AddNafathTwoFactor(
        this IServiceCollection services,
        Action<NafathOptions> configure)
    {
        var options = new NafathOptions();
        configure(options);
        return services.AddNafathTwoFactor(options);
    }

    private static IServiceCollection AddNafathTwoFactor(
        this IServiceCollection services,
        NafathOptions options)
    {
        services.AddSingleton(options);

        services.AddHttpClient<HttpNafathClient>()
            .ConfigureHttpClient(c => c.Timeout = options.Timeout);

        services.AddSingleton<INafathClient, HttpNafathClient>();
        services.AddSingleton<NafathTwoFactorChannel>();
        services.AddSingleton<ITwoFactorChannel>(sp => sp.GetRequiredService<NafathTwoFactorChannel>());

        return services;
    }
}
