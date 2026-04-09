namespace ACommerce.Notification.Operations.Abstractions;

/// <summary>
/// أولوية الإشعار
/// </summary>
public sealed class NotificationPriority
{
    public string Value { get; }
    private NotificationPriority(string value) => Value = value;

    public static readonly NotificationPriority Low = new("low");
    public static readonly NotificationPriority Normal = new("normal");
    public static readonly NotificationPriority High = new("high");
    public static readonly NotificationPriority Urgent = new("urgent");

    public static NotificationPriority Custom(string value) => new(value);
    public override string ToString() => Value;
    public static implicit operator string(NotificationPriority np) => np.Value;
}

/// <summary>
/// نوع الإشعار - يُنشئه المطور في تطبيقه.
///
/// مثال:
///   public static class AppNotifications {
///       public static readonly NotificationType NewOrder = new("new_order") {
///           Channels = { "inapp", "push" },
///           Priority = NotificationPriority.High,
///           Title = "طلب جديد"
///       };
///       public static readonly NotificationType Marketing = new("marketing") {
///           Channels = { "email" },
///           Priority = NotificationPriority.Low
///       };
///   }
///
/// ثم:
///   config.DefineType(AppNotifications.NewOrder);
///   await notifier.SendAsync(AppNotifications.NewOrder, userId, data);
/// </summary>
public class NotificationType
{
    /// <summary>
    /// معرف فريد (يُستخدم كمفتاح في التسجيل)
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// قنوات التسليم لهذا النوع
    /// </summary>
    public List<string> Channels { get; set; } = new();

    /// <summary>
    /// الأولوية الافتراضية
    /// </summary>
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

    /// <summary>
    /// عنوان افتراضي (يمكن استبداله عند الإرسال)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// رسالة افتراضية (يمكن استبدالها عند الإرسال)
    /// </summary>
    public string? Message { get; set; }

    public NotificationType(string name) => Name = name;

    public override string ToString() => Name;
    public override bool Equals(object? obj) => obj is NotificationType nt && nt.Name == Name;
    public override int GetHashCode() => Name.GetHashCode();
}
