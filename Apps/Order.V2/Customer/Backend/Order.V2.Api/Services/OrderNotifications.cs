using ACommerce.Notification.Operations.Abstractions;

namespace Order.V2.Api.Services;

public static class OrderNotifications
{
    public static readonly NotificationType NewOrder = new("new_order")
    {
        Channels = { "inapp" },
        Priority = NotificationPriority.Urgent,
        Title = "طلب جديد",
        Message = "لديك طلب جديد ينتظر الردّ"
    };

    public static readonly NotificationType OrderAccepted = new("order_accepted")
    {
        Channels = { "inapp" },
        Priority = NotificationPriority.High,
        Title = "تم قبول طلبك",
        Message = "المتجر يحضّر طلبك الآن"
    };

    public static readonly NotificationType OrderReady = new("order_ready")
    {
        Channels = { "inapp" },
        Priority = NotificationPriority.High,
        Title = "طلبك جاهز",
        Message = "تفضّل لاستلام طلبك من المتجر"
    };

    public static readonly NotificationType OrderRejected = new("order_rejected")
    {
        Channels = { "inapp" },
        Priority = NotificationPriority.Normal,
        Title = "لم يُقبل طلبك",
        Message = "المتجر لم يتمكن من قبول طلبك حالياً"
    };

    public static readonly NotificationType OrderDelivered = new("order_delivered")
    {
        Channels = { "inapp" },
        Priority = NotificationPriority.Normal,
        Title = "تم التسليم",
        Message = "شكراً لك! نتمنى لك وجبة هنيئة"
    };
}
