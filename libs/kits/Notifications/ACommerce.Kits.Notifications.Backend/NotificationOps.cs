using ACommerce.OperationEngine.Core;

namespace ACommerce.Kits.Notifications.Backend;

/// <summary>أنواع عمليّات Notifications kit — typed.</summary>
public static class NotificationOps
{
    /// <summary>
    /// إنشاء سجلّ إشعار للمستخدم. يُرسَل كعمليّة بنت من أيّ composition
    /// (مثل Chat.WithNotifications) فتمرّ بكامل دورة OAM (analyzers،
    /// parties، interceptors، SaveAtEnd) بدل استدعاء الـ store مباشرةً.
    /// </summary>
    public static readonly OperationType Create = new("notification.create");
}

/// <summary>مفاتيح وسوم الإشعارات على القيد.</summary>
public static class NotificationTagKeys
{
    public static readonly TagKey UserId       = new("notif_user_id");
    public static readonly TagKey Type         = new("notif_type");
    public static readonly TagKey Title        = new("notif_title");
    public static readonly TagKey Body         = new("notif_body");
    public static readonly TagKey RelatedId    = new("notif_related_id");
    public static readonly TagKey ParentOpId   = new("parent_op_id");
}

public static class NotificationMarkers
{
    public static readonly Marker IsNotification = new(new TagKey("kind"), new TagValue("notification"));
}
