using ACommerce.Kits.Notifications.Frontend.Customer.Widgets;

namespace ACommerce.Kits.Notifications.Frontend.Customer;

/// <summary>
/// widgets الـ Notifications المتاحة للتطبيق. الكيت لا يَفرض routes —
/// التطبيق يَختار شكل التَركيب.
/// </summary>
public static class NotificationsWidgets
{
    /// <summary>صفحة inbox كاملة (header + list + mark-all).</summary>
    public static Type Inbox       => typeof(AcNotificationsInboxWidget);

    /// <summary>شارة عَدّاد إشعارات غير مقروءة — للـ navbar/dashboard.</summary>
    public static Type UnreadBadge => typeof(AcNotificationsUnreadBadgeWidget);
}
