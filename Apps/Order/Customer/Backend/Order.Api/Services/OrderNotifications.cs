using ACommerce.Notification.Operations.Abstractions;

namespace Order.Api.Services;

/// <summary>
/// أنواع الإشعارات في تطبيق اوردر.
/// كل نوع يحدد القنوات والأولوية والعنوان الافتراضي.
/// يُسجّل في Program.cs عبر config.DefineType().
/// يُستدعى عبر notifier.SendAsync(OrderNotifications.NewOrder, recipient, data).
/// </summary>
public static class OrderNotifications
{
    /// <summary>إشعار التاجر: طلب جديد من عميل (عاجل)</summary>
    public static readonly NotificationType NewOrder = new("new_order")
    {
        Channels = { "inapp" },
        Priority = NotificationPriority.Urgent,
        Title = "طلب جديد",
        Message = "لديك طلب جديد ينتظر الردّ"
    };

    /// <summary>إشعار العميل: المتجر قبل طلبه</summary>
    public static readonly NotificationType OrderAccepted = new("order_accepted")
    {
        Channels = { "inapp" },
        Priority = NotificationPriority.High,
        Title = "تم قبول طلبك",
        Message = "المتجر يحضّر طلبك الآن"
    };

    /// <summary>إشعار العميل: الطلب جاهز للاستلام</summary>
    public static readonly NotificationType OrderReady = new("order_ready")
    {
        Channels = { "inapp" },
        Priority = NotificationPriority.High,
        Title = "طلبك جاهز",
        Message = "تفضّل لاستلام طلبك من المتجر"
    };

    /// <summary>إشعار العميل: المتجر رفض أو انتهت المهلة</summary>
    public static readonly NotificationType OrderRejected = new("order_rejected")
    {
        Channels = { "inapp" },
        Priority = NotificationPriority.Normal,
        Title = "لم يُقبل طلبك",
        Message = "المتجر لم يتمكن من قبول طلبك حالياً"
    };

    /// <summary>إشعار العميل: تم التسليم</summary>
    public static readonly NotificationType OrderDelivered = new("order_delivered")
    {
        Channels = { "inapp" },
        Priority = NotificationPriority.Normal,
        Title = "تم التسليم",
        Message = "شكراً لك! نتمنى لك وجبة هنيئة"
    };
}
