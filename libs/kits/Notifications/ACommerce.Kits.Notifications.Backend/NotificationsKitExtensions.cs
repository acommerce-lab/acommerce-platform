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
        // INotificationDispatcher: واجهة دفع OAM للإشعارات. المعترضات
        // (Chat.WithNotifications إلخ) تعتمدها بدل الـ store المباشر،
        // فينطلق envelope كامل لكلّ إشعار + يُحفظ ذرّيّاً عبر SaveAtEnd.
        services.AddScoped<INotificationDispatcher, OpEngineNotificationDispatcher>();
        services.AddControllers().AddApplicationPart(typeof(NotificationsController).Assembly);
        services.AddNotificationsKitPolicies();
        return services;
    }
}
