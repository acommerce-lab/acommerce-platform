using ACommerce.Payments.Operations.Abstractions;
using ACommerce.Payments.Providers.Noon.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Payments.Providers.Noon.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يضيف بوابة دفع Noon.
    ///
    ///   services.AddNoonPaymentGateway(configuration);
    /// </summary>
    public static IServiceCollection AddNoonPaymentGateway(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = NoonOptions.SectionName)
    {
        var options = configuration.GetSection(sectionName).Get<NoonOptions>() ?? new NoonOptions();
        return services.AddNoonPaymentGateway(options);
    }

    /// <summary>يضيف بوابة Noon مع خيارات مخصصة</summary>
    public static IServiceCollection AddNoonPaymentGateway(
        this IServiceCollection services,
        Action<NoonOptions> configure)
    {
        var options = new NoonOptions();
        configure(options);
        return services.AddNoonPaymentGateway(options);
    }

    private static IServiceCollection AddNoonPaymentGateway(
        this IServiceCollection services,
        NoonOptions options)
    {
        services.AddSingleton(options);
        services.AddHttpClient<NoonPaymentGateway>()
            .ConfigureHttpClient(c => c.Timeout = options.Timeout);

        services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<NoonPaymentGateway>());

        return services;
    }
}
