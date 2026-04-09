# ACommerce.Notifications.Abstractions

## نظرة عامة
تجريدات نظام الإشعارات. توفر واجهة موحدة لجميع قنوات الإشعارات مع دعم قنوات متعددة.

## الموقع
`/Core/ACommerce.Notifications.Abstractions`

## التبعيات
- لا توجد تبعيات خارجية (مكتبة تجريدات)

---

## الواجهات (Contracts)

### INotificationChannel
واجهة قناة الإشعارات:

```csharp
public interface INotificationChannel
{
    // نوع القناة
    NotificationChannel Channel { get; }

    // إرسال إشعار عبر هذه القناة
    Task<NotificationResult> SendAsync(
        Notification notification,
        CancellationToken cancellationToken = default);

    // التحقق من إمكانية الإرسال عبر القناة
    Task<bool> ValidateAsync(
        Notification notification,
        CancellationToken cancellationToken = default);
}
```

---

## النماذج (Models)

### Notification
نموذج الإشعار:

```csharp
public record Notification
{
    // معرف فريد للإشعار
    public Guid Id { get; init; } = Guid.NewGuid();

    // معرف المستخدم المستهدف
    public required string UserId { get; init; }

    // عنوان ورسالة الإشعار
    public required string Title { get; init; }
    public required string Message { get; init; }

    // نوع وأولوية الإشعار
    public NotificationType Type { get; init; } = NotificationType.Info;
    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;

    // التوقيتات
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ScheduledAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }

    // بيانات إضافية
    [NotMapped] public Dictionary<string, string>? Data { get; init; }

    // قنوات التسليم
    public List<ChannelDelivery> Channels { get; init; } = [];

    // رابط الإجراء
    public string? ActionUrl { get; init; }

    // صورة الإشعار
    public string? ImageUrl { get; init; }

    // صوت وBadge
    public string? Sound { get; init; }
    public int? BadgeCount { get; init; }

    // خصائص محسوبة
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow;
    public bool IsScheduled => ScheduledAt.HasValue && ScheduledAt.Value > DateTimeOffset.UtcNow;
    public bool IsFullyDelivered => Channels.All(c => c.Status == DeliveryStatus.Sent);
}
```

### NotificationResult
نتيجة إرسال الإشعار:

```csharp
public record NotificationResult
{
    public required bool Success { get; init; }
    public required Guid NotificationId { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}
```

---

## التعدادات (Enums)

### NotificationChannel

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `InApp` | 1 | داخل التطبيق (عبر SignalR) |
| `Email` | 2 | البريد الإلكتروني |
| `SMS` | 3 | رسائل نصية SMS |
| `Firebase` | 4 | Firebase Cloud Messaging |
| `WhatsApp` | 5 | WhatsApp Business API |
| `Webhook` | 6 | Webhook لنظام خارجي |

### NotificationType

| القيمة | الوصف |
|--------|-------|
| `Info` | معلومات عامة |
| `Success` | نجاح عملية |
| `Warning` | تحذير |
| `Error` | خطأ |
| `Order` | متعلق بالطلبات |
| `Payment` | متعلق بالمدفوعات |
| `Shipping` | متعلق بالشحن |
| `Chat` | محادثات |
| `Promotion` | عروض ترويجية |

### NotificationPriority

| القيمة | الوصف |
|--------|-------|
| `Low` | أولوية منخفضة |
| `Normal` | أولوية عادية |
| `High` | أولوية عالية |
| `Critical` | حرجة |

---

## بنية الملفات
```
ACommerce.Notifications.Abstractions/
├── Contracts/
│   ├── INotificationChannel.cs
│   ├── INotificationService.cs
│   └── INotificationPublisher.cs
├── Models/
│   ├── Notification.cs
│   ├── NotificationResult.cs
│   ├── NotificationEvent.cs
│   └── ChannelDelivery.cs
├── Enums/
│   ├── NotificationChannel.cs
│   ├── NotificationType.cs
│   ├── NotificationPriority.cs
│   └── DeliveryStatus.cs
└── Exceptions/
    └── NotificationException.cs
```

---

## التنفيذات المتاحة
- `ACommerce.Notifications.Channels.Email` - البريد الإلكتروني
- `ACommerce.Notifications.Channels.Firebase` - Push Notifications
- `ACommerce.Notifications.Channels.InApp` - داخل التطبيق

---

## ملاحظات تقنية

1. **Multi-Channel**: دعم إرسال الإشعار عبر قنوات متعددة
2. **Scheduling**: دعم جدولة الإشعارات
3. **Expiration**: دعم انتهاء صلاحية الإشعارات
4. **Retry Logic**: منطق إعادة المحاولة للقنوات الفاشلة
5. **Record Types**: استخدام records للـ immutability
