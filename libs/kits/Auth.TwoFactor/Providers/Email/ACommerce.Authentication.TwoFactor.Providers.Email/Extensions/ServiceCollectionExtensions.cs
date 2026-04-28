using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Authentication.TwoFactor.Providers.Email.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يضيف قناة Email 2FA مع LoggingEmailSender افتراضي.
    /// للإنتاج استخدم الحمل الذي يأخذ TSender.
    /// </summary>
    public static IServiceCollection AddEmailTwoFactor(
        this IServiceCollection services,
        Action<EmailTwoFactorOptions>? configure = null)
    {
        var options = new EmailTwoFactorOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
        services.AddSingleton<EmailTwoFactorChannel>();
        services.AddSingleton<ITwoFactorChannel>(sp => sp.GetRequiredService<EmailTwoFactorChannel>());

        return services;
    }

    /// <summary>يضيف قناة Email 2FA مع مُرسل مخصص</summary>
    public static IServiceCollection AddEmailTwoFactor<TSender>(
        this IServiceCollection services,
        Action<EmailTwoFactorOptions>? configure = null)
        where TSender : class, IEmailSender
    {
        var options = new EmailTwoFactorOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IEmailSender, TSender>();
        services.AddSingleton<EmailTwoFactorChannel>();
        services.AddSingleton<ITwoFactorChannel>(sp => sp.GetRequiredService<EmailTwoFactorChannel>());

        return services;
    }
}
