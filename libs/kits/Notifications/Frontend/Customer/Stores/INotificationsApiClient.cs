namespace ACommerce.Kits.Notifications.Frontend.Customer.Stores;

/// <summary>
/// عميل HTTP خاصّ بـ Notifications kit. يَعرف شكل الردّ من
/// <c>NotificationsController</c> ويُقَشِّر الـ envelope. صفحات + store
/// لا تَرى JSON. تَطبيقات أخرى يُمكنها استبدال هذا بتنفيذ ضدّ مَصادر مختلفة.
/// </summary>
public interface INotificationsApiClient
{
    /// <summary>GET /notifications — قائمة الإشعارات للمستخدِم الحاليّ.</summary>
    Task<IReadOnlyList<NotificationItem>> ListAsync(CancellationToken ct = default);

    /// <summary>POST /notifications/{id}/read — يُعَلّم إشعاراً واحداً مقروءاً.</summary>
    Task<bool> MarkReadAsync(string id, CancellationToken ct = default);

    /// <summary>POST /notifications/read-all — يُعَلّم كلّ الإشعارات.</summary>
    Task<bool> MarkAllReadAsync(CancellationToken ct = default);
}
