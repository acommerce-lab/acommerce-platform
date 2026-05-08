using ACommerce.Kits.Notifications.Frontend.Customer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Compositions.Customer.Notifications.Realtime;

/// <summary>
/// مَدخَل realtime: hub التَطبيق يَستَدعي <see cref="OnNotificationReceived"/>
/// عِند وُصول إشعار جَديد. تُدفَع إلى <see cref="DefaultNotificationsStore.IngestRealtimeNotification"/>
/// فيُحَدِّث الحالة فَوراً + يَزيد عَدّاد UnreadComposition.
/// </summary>
public sealed class NotificationsRealtimeIngestor
{
    private readonly DefaultNotificationsStore _store;
    public NotificationsRealtimeIngestor(INotificationsStore store)
        => _store = (DefaultNotificationsStore)store;

    public void OnNotificationReceived(NotificationItem n) =>
        _store.IngestRealtimeNotification(n);
}

public static class NotificationsRealtimeExtensions
{
    public static IServiceCollection AddNotificationsRealtimeComposition(this IServiceCollection services)
    {
        services.AddScoped<NotificationsRealtimeIngestor>();
        return services;
    }
}
