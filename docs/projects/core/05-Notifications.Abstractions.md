# ACommerce.Notifications.Abstractions

## نظرة عامة
مكتبة تجريدات نظام الإشعارات المتعددة القنوات. تدعم الإشعارات الفورية، المجدولة، وإعادة المحاولة مع قنوات متعددة (In-App, Email, SMS, Firebase, WhatsApp, Webhook).

## الموقع
`/Core/ACommerce.Notifications.Abstractions`

## التبعيات
- لا توجد تبعيات خارجية (مكتبة تجريدات)

---

## النماذج (Models)

### Notification
نموذج الإشعار الرئيسي:

```csharp
public record Notification
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string UserId { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public NotificationType Type { get; init; } = NotificationType.Info;
    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ScheduledAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public Dictionary<string, string>? Data { get; init; }
    public List<ChannelDelivery> Channels { get; init; } = [];
    public string? ActionUrl { get; init; }
    public string? ImageUrl { get; init; }
    public string? Sound { get; init; }
    public int? BadgeCount { get; init; }
}
```

### خصائص مُحسَبة

| الخاصية | النوع | الوصف |
|---------|------|-------|
| `IsExpired` | `bool` | هل انتهت صلاحية الإشعار؟ |
| `IsScheduled` | `bool` | هل الإشعار مجدول للمستقبل؟ |
| `IsFullyDelivered` | `bool` | هل تم التسليم لجميع القنوات؟ |
| `IsPartiallyDelivered` | `bool` | هل تم التسليم لقناة واحدة على الأقل؟ |
| `IsCompletelyFailed` | `bool` | هل فشل التسليم لجميع القنوات؟ |

### ChannelDelivery
تتبع حالة التسليم لكل قناة:

```csharp
public class ChannelDelivery
{
    public required NotificationChannel Channel { get; init; }
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMinutes(5);
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public string? ResponseData { get; set; }
}
```

### طرق ChannelDelivery

| الطريقة | الوصف |
|---------|-------|
| `CanRetry()` | هل يمكن إعادة المحاولة؟ |
| `RecordFailure(error)` | تسجيل فشل مع Exponential Backoff |
| `RecordSuccess(metadata, responseData)` | تسجيل نجاح التسليم |
| `MarkAsSending()` | تغيير الحالة لـ Sending |
| `MarkAsExpired()` | تغيير الحالة لـ Expired |

### Exponential Backoff
عند الفشل، يتم حساب التأخير:
```
NextRetryDelay = RetryDelay × 2^(RetryCount-1)
```
مثال: إذا `RetryDelay = 5min`:
- المحاولة 1: 5 دقائق
- المحاولة 2: 10 دقائق
- المحاولة 3: 20 دقيقة

### NotificationEvent
حدث إشعار للـ Microservices:

```csharp
public record NotificationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public required string UserId { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public NotificationType Type { get; init; } = NotificationType.Info;
    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ScheduledAt { get; init; }
    public Dictionary<string, string>? Data { get; init; }
    public List<ChannelConfiguration> Channels { get; init; } = new();
    public string? ActionUrl { get; init; }
    public string? ImageUrl { get; init; }
    public string? Sound { get; init; }

    // تحويل إلى Notification
    public Notification ToNotification();
}
```

### ChannelConfiguration
تكوين قناة الإشعار:
```csharp
public record ChannelConfiguration
{
    public required NotificationChannel Channel { get; init; }
    public int MaxRetries { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMinutes(5);
}
```

### NotificationResult
نتيجة إرسال الإشعار:
```csharp
public record NotificationResult
{
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }
    public Guid? NotificationId { get; init; }
    public List<string>? DeliveredChannels { get; init; }
    public List<string>? FailedChannels { get; init; }
}
```

---

## التعدادات (Enums)

### NotificationChannel
قنوات الإشعارات المدعومة:

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `InApp` | 1 | إشعار داخل التطبيق (SignalR) |
| `Email` | 2 | البريد الإلكتروني |
| `SMS` | 3 | رسالة نصية |
| `Firebase` | 4 | Firebase Cloud Messaging (Push) |
| `WhatsApp` | 5 | WhatsApp Business API |
| `Webhook` | 6 | Webhook لنظام خارجي |

### NotificationPriority
أولوية الإشعار:

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `Low` | 1 | منخفضة - يمكن تأخيرها |
| `Normal` | 2 | عادية - الافتراضي |
| `High` | 3 | عالية - يجب الإرسال في أسرع وقت |
| `Urgent` | 4 | عاجلة - تحتاج رداً |
| `Critical` | 5 | حرجة - يجب الوصول حتى لو أزعج المستخدم |

### NotificationType
نوع الإشعار:

| القيمة | الوصف |
|--------|-------|
| `Info` | معلومات عامة |
| `Success` | عملية ناجحة |
| `Warning` | تحذير |
| `Error` | خطأ |
| `NafathVerification` | التحقق من نفاذ |
| `ChatMessage` | رسالة محادثة |
| `OfferUpdate` | تحديث عرض |
| `NewBooking` | حجز جديد |
| `BookingUpdate` | تحديث حجز |
| `NewReview` | تقييم جديد |
| `SystemAlert` | تنبيه نظام |
| `Welcome` | ترحيب |
| `Reminder` | تذكير |
| `Promotion` | عرض ترويجي |
| `AccountUpdate` | تحديث حساب |
| `SecurityAlert` | تنبيه أمني |
| `Payment` | دفع |
| `Custom` | مخصص |

### DeliveryStatus
حالة تسليم الإشعار:

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `Pending` | 1 | في الانتظار |
| `Sending` | 2 | جارٍ الإرسال |
| `Sent` | 3 | تم الإرسال بنجاح |
| `Failed` | 4 | فشل الإرسال |
| `Read` | 5 | تم القراءة |
| `Dismissed` | 6 | تم التجاهل |
| `Expired` | 7 | انتهت الصلاحية |

---

## الواجهات (Contracts)

### INotificationService
الخدمة الرئيسية لإرسال الإشعارات:

```csharp
public interface INotificationService
{
    // إرسال إشعار فوري
    Task<NotificationResult> SendAsync(
        Notification notification,
        CancellationToken cancellationToken = default);

    // إرسال مجموعة إشعارات
    Task<List<NotificationResult>> SendBatchAsync(
        IEnumerable<Notification> notifications,
        CancellationToken cancellationToken = default);

    // جدولة إشعار لوقت لاحق
    Task<NotificationResult> ScheduleAsync(
        Notification notification,
        CancellationToken cancellationToken = default);
}
```

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

    // التحقق من صحة الإشعار لهذه القناة
    Task<bool> ValidateAsync(
        Notification notification,
        CancellationToken cancellationToken = default);
}
```

### INotificationPublisher
ناشر الإشعارات (للـ Microservices):

```csharp
public interface INotificationPublisher
{
    // نشر إشعار إلى Message Queue
    Task PublishAsync(
        NotificationEvent notificationEvent,
        CancellationToken cancellationToken = default);

    // نشر مجموعة إشعارات
    Task PublishBatchAsync(
        IEnumerable<NotificationEvent> notificationEvents,
        CancellationToken cancellationToken = default);
}
```

---

## الاستثناءات (Exceptions)

### NotificationException
استثناء عام للإشعارات:
```csharp
public class NotificationException : Exception
{
    public string ErrorCode { get; }
    public NotificationChannel? Channel { get; }
}
```

### ChannelDeliveryException
استثناء فشل التسليم لقناة:
```csharp
public class ChannelDeliveryException : NotificationException
{
    // ErrorCode = "CHANNEL_DELIVERY_FAILED"
}
```

---

## بنية الملفات
```
ACommerce.Notifications.Abstractions/
├── Contracts/
│   ├── INotificationChannel.cs
│   ├── INotificationPublisher.cs
│   └── INotificationService.cs
├── Models/
│   ├── Notification.cs
│   ├── ChannelDelivery.cs
│   ├── NotificationEvent.cs
│   └── NotificationResult.cs
├── Enums/
│   ├── NotificationChannel.cs
│   ├── NotificationType.cs
│   ├── NotificationPriority.cs
│   └── DeliveryStatus.cs
└── Exceptions/
    └── NotificationException.cs
```

---

## مثال استخدام كامل

### إرسال إشعار فوري
```csharp
var notification = new Notification
{
    UserId = userId.ToString(),
    Title = "تم شحن طلبك",
    Message = "طلبك رقم #12345 في طريقه إليك",
    Type = NotificationType.Info,
    Priority = NotificationPriority.Normal,
    ActionUrl = "/orders/12345",
    ImageUrl = "https://example.com/shipping.png",
    Data = new Dictionary<string, string>
    {
        ["orderId"] = "12345",
        ["trackingNumber"] = "SA123456789"
    },
    Channels = new List<ChannelDelivery>
    {
        new() { Channel = NotificationChannel.InApp },
        new() { Channel = NotificationChannel.Firebase },
        new() { Channel = NotificationChannel.Email }
    }
};

var result = await notificationService.SendAsync(notification);

if (result.Success)
{
    Console.WriteLine($"تم الإرسال إلى: {string.Join(", ", result.DeliveredChannels)}");
}
```

### جدولة إشعار
```csharp
var scheduledNotification = new Notification
{
    UserId = userId.ToString(),
    Title = "تذكير: لديك عرض ينتهي اليوم!",
    Message = "خصم 30% ينتهي الليلة",
    Type = NotificationType.Reminder,
    Priority = NotificationPriority.High,
    ScheduledAt = DateTimeOffset.UtcNow.AddHours(2),
    ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
    Channels = new List<ChannelDelivery>
    {
        new() { Channel = NotificationChannel.Firebase },
        new() { Channel = NotificationChannel.SMS, MaxRetries = 5 }
    }
};

await notificationService.ScheduleAsync(scheduledNotification);
```

### نشر حدث (Microservices)
```csharp
var notificationEvent = new NotificationEvent
{
    UserId = userId.ToString(),
    Title = "طلب جديد",
    Message = "لديك طلب جديد يحتاج مراجعة",
    Type = NotificationType.NewBooking,
    Priority = NotificationPriority.Urgent,
    Channels = new List<ChannelConfiguration>
    {
        new() { Channel = NotificationChannel.InApp },
        new() { Channel = NotificationChannel.Firebase },
        new() { Channel = NotificationChannel.WhatsApp, MaxRetries = 5, RetryDelay = TimeSpan.FromMinutes(10) }
    }
};

await notificationPublisher.PublishAsync(notificationEvent);
```

---

## ملاحظات تقنية

1. **Immutable Records**: `Notification` و `NotificationResult` هي records للـ immutability
2. **Retry Logic**: دعم Exponential Backoff للإعادة المحاولة
3. **Multi-Channel**: إرسال لعدة قنوات في وقت واحد
4. **Scheduling**: دعم جدولة الإشعارات
5. **Expiration**: دعم انتهاء صلاحية الإشعارات
6. **Microservices Ready**: `INotificationPublisher` للـ Message Queue
