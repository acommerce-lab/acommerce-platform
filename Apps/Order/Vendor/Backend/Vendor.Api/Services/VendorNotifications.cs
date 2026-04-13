using ACommerce.Notification.Operations.Abstractions;

namespace Vendor.Api.Services;

/// <summary>
/// أنواع الإشعارات في خدمة التاجر.
/// تُسجّل في Program.cs وتُستدعى عبر Notifier.SendAsync().
/// </summary>
public static class VendorNotifications
{
    /// <summary>طلب جديد وصل من المنصة</summary>
    public static readonly NotificationType OrderReceived = new("vendor.order_received")
    {
        Channels = { "inapp" },
        Priority = NotificationPriority.Urgent,
        Title = "طلب جديد",
        Message = "لديك طلب جديد — أسرع بالرد"
    };

    /// <summary>طلب تم تجاوز مهلته</summary>
    public static readonly NotificationType OrderTimedOut = new("vendor.order_timeout")
    {
        Channels = { "inapp" },
        Priority = NotificationPriority.High,
        Title = "انتهت مهلة الطلب",
        Message = "لم تردّ في الوقت المحدد — تم إلغاء الطلب تلقائياً"
    };
}
