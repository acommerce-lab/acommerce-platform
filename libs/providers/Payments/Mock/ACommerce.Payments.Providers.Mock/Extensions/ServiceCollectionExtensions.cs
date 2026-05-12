using ACommerce.Payments.Operations.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Payments.Providers.Mock.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يُسَجِّل MockPaymentGateway كَـ <see cref="IPaymentGateway"/> الافتِراضي.
    /// لِلإنتاج: استَبدِله بِـ <c>services.AddMoyasar(...)</c> أَو
    /// <c>services.AddNoon(...)</c>.
    /// </summary>
    public static IServiceCollection AddMockPayment(this IServiceCollection services)
    {
        services.AddOptions<MockPaymentOptions>();
        return Register(services);
    }

    /// <summary>تَكوين بِـ delegate.</summary>
    public static IServiceCollection AddMockPayment(
        this IServiceCollection services,
        Action<MockPaymentOptions> configure)
    {
        services.Configure(configure);
        return Register(services);
    }

    /// <summary>تَكوين مَن قِسم <see cref="IConfigurationSection"/> (مَثَلاً <c>cfg.GetSection("MockPayment")</c>).</summary>
    public static IServiceCollection AddMockPayment(
        this IServiceCollection services,
        IConfigurationSection configSection)
    {
        services.Configure<MockPaymentOptions>(configSection);
        return Register(services);
    }

    private static IServiceCollection Register(IServiceCollection services)
    {
        services.AddSingleton<MockPaymentGateway>();
        services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<MockPaymentGateway>());
        return services;
    }
}
