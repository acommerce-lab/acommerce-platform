# ACommerce.Notifications.Channels

## نظرة عامة
تنفيذات قنوات الإشعارات المختلفة. كل قناة تنفذ واجهة `INotificationChannel`.

---

## قناة البريد الإلكتروني (Email)

### الموقع
`/Other/ACommerce.Notifications.Channels.Email`

### التبعيات
- `ACommerce.Notifications.Abstractions`
- `MailKit` - مكتبة SMTP

### الوصف
إرسال إشعارات عبر البريد الإلكتروني مع دعم قوالب HTML.

### EmailOptions
```csharp
public class EmailOptions
{
    public const string SectionName = "Notifications:Email";

    // مزود البريد
    public EmailProvider Provider { get; set; } = EmailProvider.SMTP;

    // إعدادات SMTP
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public required string Username { get; set; }
    public required string Password { get; set; }
    public bool EnableSsl { get; set; } = true;

    // عناوين المرسل
    public required string FromAddress { get; set; }
    public string FromName { get; set; } = "ACommerce";
    public string? ReplyToAddress { get; set; }

    // القوالب
    public bool EnableHtmlTemplates { get; set; } = true;
    public string TemplatesPath { get; set; } = "EmailTemplates";
    public string DefaultTemplate { get; set; } = "Default";
}
```

### مزودو البريد المدعومون
| المزود | الوصف |
|--------|-------|
| `SMTP` | SMTP عام |
| `Gmail` | Gmail |
| `SendGrid` | SendGrid API |
| `AwsSes` | AWS SES |
| `AzureEmailService` | Azure Communication Services |

### مثال التكوين
```json
{
  "Notifications": {
    "Email": {
      "Provider": "SMTP",
      "Host": "smtp.gmail.com",
      "Port": 587,
      "Username": "your-email@gmail.com",
      "Password": "app-password",
      "EnableSsl": true,
      "FromAddress": "noreply@example.com",
      "FromName": "ACommerce",
      "EnableHtmlTemplates": true
    }
  }
}
```

---

## قناة Firebase (Push Notifications)

### الموقع
`/Other/ACommerce.Notifications.Channels.Firebase`

### التبعيات
- `ACommerce.Notifications.Abstractions`
- `FirebaseAdmin` - Firebase Admin SDK

### الوصف
إرسال Push Notifications عبر Firebase Cloud Messaging لأجهزة Android و iOS.

### FirebaseOptions
```csharp
public class FirebaseOptions
{
    public const string SectionName = "Notifications:Firebase";

    // مفتاح Service Account
    public string? ServiceAccountKeyPath { get; set; }
    public string? ServiceAccountKeyJson { get; set; }
    public string? ProjectId { get; set; }

    // إعدادات الرسائل
    public FirebaseMessagePriority DefaultPriority { get; set; } = FirebaseMessagePriority.High;
    public int TimeToLiveSeconds { get; set; } = 86400; // 24 ساعة
    public string DefaultSound { get; set; } = "default";
    public string? DefaultColor { get; set; } = "#667eea";
    public string? DefaultIcon { get; set; }
    public string DefaultChannelId { get; set; } = "default";

    // خيارات متقدمة
    public bool EnableBadge { get; set; } = true;
    public bool EnableCollapseKey { get; set; } = false;
    public int MaxBatchSize { get; set; } = 500;
    public bool DryRun { get; set; } = false;
}
```

### IFirebaseTokenStore
واجهة تخزين Device Tokens:

```csharp
public interface IFirebaseTokenStore
{
    Task<List<FirebaseDeviceToken>> GetUserTokensAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task SaveTokenAsync(
        FirebaseDeviceToken token,
        CancellationToken cancellationToken = default);

    Task DeactivateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);
}
```

### مثال التكوين
```json
{
  "Notifications": {
    "Firebase": {
      "ServiceAccountKeyPath": "firebase-service-account.json",
      "DefaultPriority": "High",
      "TimeToLiveSeconds": 86400,
      "DefaultSound": "default",
      "DefaultColor": "#667eea",
      "DefaultChannelId": "orders"
    }
  }
}
```

### الميزات
- دعم Android و iOS
- Multicast لعدة أجهزة
- إدارة Tokens التالفة
- دعم الصور والأصوات المخصصة

---

## قناة داخل التطبيق (InApp)

### الموقع
`/Other/ACommerce.Notifications.Channels.InApp`

### التبعيات
- `ACommerce.Notifications.Abstractions`
- `ACommerce.Realtime.Abstractions` - SignalR Hub

### الوصف
إرسال إشعارات لحظية داخل التطبيق عبر SignalR.

### InAppNotificationOptions
```csharp
public class InAppNotificationOptions
{
    public const string SectionName = "Notifications:InApp";

    // اسم Method في SignalR Hub
    public string MethodName { get; set; } = "ReceiveNotification";

    // إرسال عدد الإشعارات غير المقروءة
    public bool SendBadgeCount { get; set; } = true;
    public string BadgeCountMethodName { get; set; } = "UpdateBadgeCount";
}
```

### مثال التكوين
```json
{
  "Notifications": {
    "InApp": {
      "MethodName": "ReceiveNotification",
      "SendBadgeCount": true,
      "BadgeCountMethodName": "UpdateBadgeCount"
    }
  }
}
```

### Payload المرسل
```javascript
{
  id: "guid",
  userId: "user-id",
  title: "عنوان الإشعار",
  message: "محتوى الإشعار",
  type: "Order",
  priority: "High",
  createdAt: "2024-01-01T12:00:00Z",
  actionUrl: "/orders/123",
  imageUrl: "/images/order.png",
  sound: "notification",
  badgeCount: 5,
  data: { orderId: "123" }
}
```

---

## تسجيل الخدمات

### تسجيل جميع القنوات
```csharp
services.AddNotificationChannels(options =>
{
    options.UseEmail(config.GetSection("Notifications:Email"));
    options.UseFirebase(config.GetSection("Notifications:Firebase"));
    options.UseInApp();
});
```

### تسجيل قناة محددة
```csharp
// Email فقط
services.AddEmailNotificationChannel(options =>
{
    options.Host = "smtp.gmail.com";
    options.Port = 587;
    options.Username = "email@example.com";
    options.Password = "password";
    options.FromAddress = "noreply@example.com";
});

// Firebase فقط
services.AddFirebaseNotificationChannel(options =>
{
    options.ServiceAccountKeyPath = "firebase-key.json";
});

// InApp فقط
services.AddInAppNotificationChannel();
```

---

## مثال استخدام

### إرسال إشعار عبر قنوات متعددة
```csharp
var notification = new Notification
{
    UserId = userId,
    Title = "طلب جديد",
    Message = "تم استلام طلبك رقم #123",
    Type = NotificationType.Order,
    Priority = NotificationPriority.High,
    ActionUrl = "/orders/123",
    Data = new Dictionary<string, string>
    {
        ["orderId"] = "123",
        ["email"] = "user@example.com"
    },
    Channels =
    [
        new ChannelDelivery { Channel = NotificationChannel.InApp },
        new ChannelDelivery { Channel = NotificationChannel.Firebase },
        new ChannelDelivery { Channel = NotificationChannel.Email }
    ]
};

await notificationService.SendAsync(notification);
```

---

## ملاحظات تقنية

1. **Unified Interface**: جميع القنوات تنفذ `INotificationChannel`
2. **Token Management**: إدارة تلقائية لـ Firebase Tokens
3. **Template Support**: دعم قوالب HTML للبريد
4. **Real-time**: إشعارات لحظية عبر SignalR
5. **Error Handling**: معالجة أخطاء شاملة لكل قناة
6. **Logging**: تسجيل مفصل لجميع العمليات
