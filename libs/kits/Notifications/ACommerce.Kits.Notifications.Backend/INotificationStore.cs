namespace ACommerce.Kits.Notifications.Backend;

/// <summary>
/// منفذ بيانات إشعارات الـ inbox. التطبيق ينفّذها مقابل تخزينه (in-memory،
/// EF، …). الكيان <see cref="NotificationItem"/> هو عقد كحدّ أدنى — كيان
/// النطاق في التطبيق يستطيع مدّ هذا الـ record بمزيد من الحقول لكن يبقى
/// قادراً على التحويل لـ NotificationItem للعرض.
/// </summary>
public interface INotificationStore
{
    Task<IReadOnlyList<NotificationItem>> ListAsync(string userId, CancellationToken ct);

    /// <summary>يضع <c>IsRead=true</c> على عنصر واحد. <c>false</c> لو غير موجود/ليس مالكاً.</summary>
    Task<bool> MarkReadAsync(string userId, string notificationId, CancellationToken ct);

    /// <summary>يضع <c>IsRead=true</c> على كلّ عناصر المستخدم. يرجع عدد المتأثّرين.</summary>
    Task<int> MarkAllReadAsync(string userId, CancellationToken ct);
}

/// <summary>
/// عرض الإشعار في الـ inbox. الكيان الفعليّ في التطبيق قد يحوي حقولاً
/// إضافيّة (Channel-specific payload، delivery status …) لكنّ هذه الحقول
/// السبعة هي ما يهمّ الـ UI.
/// </summary>
public sealed record NotificationItem(
    string   Id,
    string   Type,         // "message" | "booking" | "review" | "system" | ...
    string   Title,
    string   Body,
    DateTime CreatedAt,
    bool     IsRead,
    string?  RelatedId);   // مثلاً ConversationId / BookingId — للتنقّل من الإشعار
