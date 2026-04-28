using ACommerce.Notification.Operations.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Notification.Providers.InApp.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يضيف قناة الإشعارات داخل التطبيق.
    /// يعتمد على وجود IRealtimeTransport مُسجّل في DI (أي مزود).
    ///
    /// الاستخدام:
    ///   services.AddSignalRRealtimeTransport&lt;MyHub, IClient&gt;();  // أو InMemory
    ///   services.AddInAppNotificationChannel();
    /// </summary>
    public static IServiceCollection AddInAppNotificationChannel(
        this IServiceCollection services,
        Action<InAppOptions>? configure = null)
    {
        var options = new InAppOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<InAppNotificationChannel>();
        services.AddSingleton<INotificationChannel>(sp => sp.GetRequiredService<InAppNotificationChannel>());

        return services;
    }
}
