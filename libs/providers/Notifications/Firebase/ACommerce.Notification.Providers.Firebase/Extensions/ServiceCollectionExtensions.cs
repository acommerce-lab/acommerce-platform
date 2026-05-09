using ACommerce.Notification.Operations.Abstractions;
using ACommerce.Notification.Providers.Firebase.Options;
using ACommerce.Notification.Providers.Firebase.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Notification.Providers.Firebase.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// يضيف قناة Firebase Cloud Messaging.
    /// يستخدم InMemoryDeviceTokenStore افتراضياً - استبدله بمخزن دائم للإنتاج.
    ///
    /// الاستخدام:
    ///   services.AddFirebaseNotificationChannel(configuration);
    ///   // أو مع مخزن مخصص:
    ///   services.AddSingleton&lt;IDeviceTokenStore, EfDeviceTokenStore&gt;();
    ///   services.AddFirebaseNotificationChannel(configuration);
    /// </summary>
    public static IServiceCollection AddFirebaseNotificationChannel(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = FirebaseOptions.SectionName)
    {
        var options = configuration.GetSection(sectionName).Get<FirebaseOptions>() ?? new FirebaseOptions();
        return services.AddFirebaseNotificationChannel(options);
    }

    /// <summary>يضيف قناة Firebase مع خيارات مخصصة</summary>
    public static IServiceCollection AddFirebaseNotificationChannel(
        this IServiceCollection services,
        Action<FirebaseOptions> configure)
    {
        var options = new FirebaseOptions();
        configure(options);
        return services.AddFirebaseNotificationChannel(options);
    }

    private static IServiceCollection AddFirebaseNotificationChannel(
        this IServiceCollection services,
        FirebaseOptions options)
    {
        services.AddSingleton(options);

        // مخزن افتراضي - يمكن للمطور تجاوزه قبل أو بعد
        services.TryAdd(ServiceDescriptor.Singleton<IDeviceTokenStore, InMemoryDeviceTokenStore>());

        services.AddSingleton<FirebaseNotificationChannel>();
        services.AddSingleton<INotificationChannel>(sp => sp.GetRequiredService<FirebaseNotificationChannel>());

        return services;
    }

    private static void TryAdd(this IServiceCollection services, ServiceDescriptor descriptor)
    {
        if (!services.Any(s => s.ServiceType == descriptor.ServiceType))
            services.Add(descriptor);
    }
}
