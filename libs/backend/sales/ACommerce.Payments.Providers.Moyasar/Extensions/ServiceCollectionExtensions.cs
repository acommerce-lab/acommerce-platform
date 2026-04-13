using ACommerce.Payments.Operations.Abstractions;
using ACommerce.Payments.Providers.Moyasar.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Payments.Providers.Moyasar.Extensions;

/// <summary>DI registration for Moyasar payment gateway.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMoyasarPayments(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("Moyasar").Get<MoyasarOptions>() ?? new MoyasarOptions();
        services.AddSingleton(options);
        services.AddHttpClient<MoyasarPaymentGateway>();
        services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<MoyasarPaymentGateway>());
        return services;
    }
}
