using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Authentication.TwoFactor.Providers.Nafath.Mock.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يُسَجِّل قَناة نَفاذ تَجريبيَّة كَـ <see cref="ITwoFactorChannel"/>
    /// مَع <see cref="MockNafathOptions"/> الافتِراضِيَّة (DisplayCode="00"،
    /// AutoVerifySeconds=10).
    ///
    /// لِلإنتاج: استَبدِلها بِـ <c>services.AddNafathTwoFactor(cfg)</c> مِن
    /// <c>ACommerce.Authentication.TwoFactor.Providers.Nafath</c>.
    /// </summary>
    public static IServiceCollection AddMockNafathTwoFactor(this IServiceCollection services)
    {
        services.AddOptions<MockNafathOptions>();
        return RegisterChannel(services);
    }

    /// <summary>
    /// يُسَجِّل قَناة نَفاذ تَجريبيَّة بِخِيارات مَخصَّصَة عَبر delegate.
    /// <code>
    /// services.AddMockNafathTwoFactor(opts =>
    /// {
    ///     opts.DisplayCode       = "00";   // الرَقَم في تَطبيق نَفاذ التَجريبي
    ///     opts.AutoVerifySeconds = 5;      // تَسريع التَّحَقُّق في dev
    /// });
    /// </code>
    /// </summary>
    public static IServiceCollection AddMockNafathTwoFactor(
        this IServiceCollection services,
        Action<MockNafathOptions> configure)
    {
        services.Configure(configure);
        return RegisterChannel(services);
    }

    /// <summary>
    /// يُسَجِّل قَناة نَفاذ تَجريبيَّة بِخِيارات مَقروءَة مِن قِسم
    /// <see cref="IConfigurationSection"/> (مَثَلاً <c>config.GetSection("MockNafath")</c>).
    /// </summary>
    public static IServiceCollection AddMockNafathTwoFactor(
        this IServiceCollection services,
        IConfigurationSection configSection)
    {
        services.Configure<MockNafathOptions>(configSection);
        return RegisterChannel(services);
    }

    private static IServiceCollection RegisterChannel(IServiceCollection services)
    {
        services.AddSingleton<MockNafathTwoFactorChannel>();
        services.AddSingleton<ITwoFactorChannel>(sp => sp.GetRequiredService<MockNafathTwoFactorChannel>());
        return services;
    }
}
