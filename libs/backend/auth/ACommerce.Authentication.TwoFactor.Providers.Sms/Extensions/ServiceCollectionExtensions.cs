using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Authentication.TwoFactor.Providers.Sms.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يضيف قناة SMS 2FA تجريبية مع LoggingSmsSender.
    /// للإنتاج: سجّل ISmsSender مخصصاً قبل استدعاء هذه.
    /// </summary>
    public static IServiceCollection AddSmsTwoFactor(this IServiceCollection services)
    {
        services.AddSingleton<ISmsSender, LoggingSmsSender>();
        services.AddSingleton<SmsTwoFactorChannel>();
        services.AddSingleton<ITwoFactorChannel>(sp => sp.GetRequiredService<SmsTwoFactorChannel>());
        return services;
    }

    /// <summary>يضيف قناة SMS 2FA مع مُرسل مخصص</summary>
    public static IServiceCollection AddSmsTwoFactor<TSender>(this IServiceCollection services)
        where TSender : class, ISmsSender
    {
        services.AddSingleton<ISmsSender, TSender>();
        services.AddSingleton<SmsTwoFactorChannel>();
        services.AddSingleton<ITwoFactorChannel>(sp => sp.GetRequiredService<SmsTwoFactorChannel>());
        return services;
    }
}
