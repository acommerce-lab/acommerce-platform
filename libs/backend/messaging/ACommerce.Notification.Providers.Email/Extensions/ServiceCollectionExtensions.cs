using ACommerce.Notification.Operations.Abstractions;
using ACommerce.Notification.Providers.Email.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Notification.Providers.Email.Extensions;

/// <summary>DI registration for the email notification channel.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmailNotifications(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
        services.AddSingleton(options);
        services.AddSingleton<INotificationChannel, EmailNotificationChannel>();
        return services;
    }
}
