using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Notifications.Backend;

public static class NotificationsKitExtensions
{
    /// <summary>
    /// يسجّل Notifications inbox kit. Push delivery (InApp / Firebase / Email)
    /// تأتي من <c>Notification.Operations</c> + مزوّداتها — هذه الـ Kit تضيف
    /// inbox API فقط.
    ///
    /// <para>الاستخدام:</para>
    /// <code>
    /// builder.Services.AddInAppNotificationChannel(o => o.MethodName = "ReceiveNotification");
    /// builder.Services.AddNotificationsKit&lt;EjarNotificationStore&gt;();
    /// </code>
    /// </summary>
    public static IServiceCollection AddNotificationsKit<TStore>(this IServiceCollection services)
        where TStore : class, INotificationStore
    {
        services.AddScoped<INotificationStore, TStore>();
        services.AddControllers().AddApplicationPart(typeof(NotificationsController).Assembly);
        return services;
    }
}
