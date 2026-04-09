# ACommerce.Notifications.Messaging

## نظرة عامة
تكامل نظام الإشعارات مع نظام Messaging. يسمح بإرسال الإشعارات عبر Message Bus.

## الموقع
`/ACommerce.Notifications.Messaging`

## التبعيات
- `ACommerce.Notifications.Abstractions`
- `ACommerce.Messaging.Abstractions`
- `Microsoft.Extensions.Hosting`

---

## Commands

### SendNotificationCommand
أمر إرسال إشعار عبر Message Bus:

```csharp
public record SendNotificationCommand
{
    // المستخدم المستهدف
    public required string UserId { get; init; }

    // عنوان ومحتوى الإشعار
    public required string Title { get; init; }
    public required string Message { get; init; }

    // نوع وأولوية الإشعار
    public NotificationType Type { get; init; } = NotificationType.Info;
    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;

    // قنوات الإرسال
    public List<NotificationChannel> Channels { get; init; } = [NotificationChannel.Email];

    // بيانات إضافية
    public Dictionary<string, object>? Data { get; init; }
}
```

---

## Handlers

### NotificationMessagingHandler
خدمة خلفية تستمع لأوامر الإشعارات:

```csharp
public class NotificationMessagingHandler : BackgroundService
{
    // يستمع على: command.notify.send

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await consumer.SubscribeAsync<SendNotificationCommand>(
            TopicNames.Command("notify", "send"),
            HandleSendNotificationCommand,
            stoppingToken);
    }
}
```

### NotificationProfileSyncHandler
مزامنة ملفات تعريف الإشعارات:

```csharp
public class NotificationProfileSyncHandler : BackgroundService
{
    // يستمع لأحداث المستخدمين لمزامنة بيانات الإشعارات
}
```

---

## Topics

| Topic | النوع | الوصف |
|-------|------|-------|
| `command.notify.send` | Command | إرسال إشعار |
| `event.user.created` | Event | مستخدم جديد (للمزامنة) |
| `event.user.updated` | Event | تحديث مستخدم |

---

## بنية الملفات
```
ACommerce.Notifications.Messaging/
├── Commands/
│   └── SendNotificationCommand.cs
├── Handlers/
│   ├── NotificationMessagingHandler.cs
│   └── NotificationProfileSyncHandler.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

---

## تسجيل الخدمات

```csharp
services.AddNotificationsMessaging();
```

يسجل:
- `NotificationMessagingHandler` - معالج أوامر الإشعارات
- `NotificationProfileSyncHandler` - مزامنة البيانات

---

## مثال استخدام

### إرسال إشعار عبر Message Bus
```csharp
public class OrderService
{
    private readonly IMessagePublisher _publisher;

    public async Task NotifyOrderCreatedAsync(Order order)
    {
        var command = new SendNotificationCommand
        {
            UserId = order.CustomerId,
            Title = "طلب جديد",
            Message = $"تم استلام طلبك رقم #{order.OrderNumber}",
            Type = NotificationType.Order,
            Priority = NotificationPriority.High,
            Channels = [
                NotificationChannel.Email,
                NotificationChannel.Firebase,
                NotificationChannel.InApp
            ],
            Data = new Dictionary<string, object>
            {
                ["orderId"] = order.Id.ToString(),
                ["orderNumber"] = order.OrderNumber,
                ["email"] = order.CustomerEmail
            }
        };

        await _publisher.PublishAsync(
            TopicNames.Command("notify", "send"),
            command);
    }
}
```

### إرسال إشعار بسيط
```csharp
await _publisher.PublishAsync(
    TopicNames.Command("notify", "send"),
    new SendNotificationCommand
    {
        UserId = "user@example.com",
        Title = "تنبيه",
        Message = "لديك رسالة جديدة",
        Channels = [NotificationChannel.Email]
    });
```

---

## سيناريوهات الاستخدام

### 1. إشعار طلب جديد
```csharp
new SendNotificationCommand
{
    UserId = customerId,
    Title = "طلب جديد",
    Message = "تم استلام طلبك بنجاح",
    Type = NotificationType.Order,
    Channels = [
        NotificationChannel.Email,
        NotificationChannel.Firebase
    ],
    Data = new { orderId, orderNumber }
}
```

### 2. إشعار شحن
```csharp
new SendNotificationCommand
{
    UserId = customerId,
    Title = "طلبك في الطريق",
    Message = "تم شحن طلبك ويتم توصيله خلال 2-3 أيام",
    Type = NotificationType.Shipping,
    Channels = [NotificationChannel.Firebase, NotificationChannel.InApp],
    Data = new { trackingNumber, estimatedDelivery }
}
```

### 3. إشعار دفع
```csharp
new SendNotificationCommand
{
    UserId = customerId,
    Title = "تم استلام الدفعة",
    Message = "تم تأكيد دفعتك بنجاح",
    Type = NotificationType.Payment,
    Priority = NotificationPriority.High,
    Channels = [NotificationChannel.Email]
}
```

---

## ملاحظات تقنية

1. **Async Processing**: معالجة غير متزامنة للإشعارات
2. **Retry Logic**: إعادة المحاولة تلقائياً عند الفشل
3. **Multi-Channel**: إرسال عبر قنوات متعددة في أمر واحد
4. **Decoupled**: فصل كامل بين المرسل والمستقبل
5. **Profile Sync**: مزامنة تلقائية لبيانات المستخدمين
