# ACommerce.Realtime.Abstractions

## نظرة عامة
مكتبة تجريدات للاتصال اللحظي (Real-time) باستخدام SignalR. توفر واجهات موحدة للإرسال والاستقبال بين الخادم والعملاء.

## الموقع
`/Core/ACommerce.Realtime.Abstractions`

## التبعيات
- لا توجد تبعيات خارجية (مكتبة تجريدات)

---

## الواجهات (Contracts)

### IRealtimeHub
واجهة خدمة الـ Hub للإرسال اللحظي:

```csharp
public interface IRealtimeHub
{
    // إرسال لمستخدم محدد
    Task SendToUserAsync(
        string userId,
        string method,
        object data,
        CancellationToken cancellationToken = default);

    // إرسال لمجموعة
    Task SendToGroupAsync(
        string groupName,
        string method,
        object data,
        CancellationToken cancellationToken = default);

    // إرسال للجميع
    Task SendToAllAsync(
        string method,
        object data,
        CancellationToken cancellationToken = default);

    // إضافة مستخدم لمجموعة
    Task AddToGroupAsync(
        string userId,
        string groupName,
        CancellationToken cancellationToken = default);

    // إزالة مستخدم من مجموعة
    Task RemoveFromGroupAsync(
        string userId,
        string groupName,
        CancellationToken cancellationToken = default);
}
```

### الطرق المتاحة

| الطريقة | الوصف | الاستخدام |
|---------|-------|----------|
| `SendToUserAsync` | إرسال لمستخدم واحد | إشعار شخصي، رسالة خاصة |
| `SendToGroupAsync` | إرسال لمجموعة | محادثة جماعية، غرفة بائع |
| `SendToAllAsync` | إرسال للجميع | إعلان عام، تحديث نظام |
| `AddToGroupAsync` | إضافة لمجموعة | انضمام لمحادثة |
| `RemoveFromGroupAsync` | إزالة من مجموعة | مغادرة محادثة |

### IRealtimeClient
واجهة العميل لاستقبال الرسائل:

```csharp
public interface IRealtimeClient
{
    // استقبال رسالة
    Task ReceiveMessage(string method, object data);
}
```

---

## النماذج (Models)

### RealtimeMessage
نموذج الرسالة اللحظية:

```csharp
public record RealtimeMessage
{
    public required string Method { get; init; }
    public required object Data { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string>? Metadata { get; init; }
}
```

| الخاصية | النوع | الوصف |
|---------|------|-------|
| `Method` | `string` | اسم الطريقة (Event Name) |
| `Data` | `object` | البيانات المُرسلة |
| `Timestamp` | `DateTimeOffset` | وقت الإرسال (UTC) |
| `Metadata` | `Dictionary<string, string>?` | بيانات إضافية |

---

## بنية الملفات
```
ACommerce.Realtime.Abstractions/
├── Contracts/
│   ├── IRealtimeHub.cs
│   └── IRealtimeClient.cs
└── Models/
    └── RealtimeMessage.cs
```

---

## مثال استخدام

### تنفيذ IRealtimeHub
```csharp
public class SignalRRealtimeHub : IRealtimeHub
{
    private readonly IHubContext<MainHub, IRealtimeClient> _hubContext;

    public SignalRRealtimeHub(IHubContext<MainHub, IRealtimeClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendToUserAsync(
        string userId,
        string method,
        object data,
        CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.User(userId).ReceiveMessage(method, data);
    }

    public async Task SendToGroupAsync(
        string groupName,
        string method,
        object data,
        CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(groupName).ReceiveMessage(method, data);
    }

    public async Task SendToAllAsync(
        string method,
        object data,
        CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.All.ReceiveMessage(method, data);
    }

    public async Task AddToGroupAsync(
        string userId,
        string groupName,
        CancellationToken cancellationToken = default)
    {
        var connectionId = GetConnectionIdForUser(userId);
        await _hubContext.Groups.AddToGroupAsync(connectionId, groupName, cancellationToken);
    }

    public async Task RemoveFromGroupAsync(
        string userId,
        string groupName,
        CancellationToken cancellationToken = default)
    {
        var connectionId = GetConnectionIdForUser(userId);
        await _hubContext.Groups.RemoveFromGroupAsync(connectionId, groupName, cancellationToken);
    }
}
```

### استخدام في خدمة
```csharp
public class OrderNotificationService
{
    private readonly IRealtimeHub _realtimeHub;

    public OrderNotificationService(IRealtimeHub realtimeHub)
    {
        _realtimeHub = realtimeHub;
    }

    public async Task NotifyOrderStatusChanged(Guid orderId, string customerId, string status)
    {
        var message = new
        {
            OrderId = orderId,
            Status = status,
            UpdatedAt = DateTime.UtcNow
        };

        // إشعار المشتري
        await _realtimeHub.SendToUserAsync(customerId, "OrderStatusChanged", message);

        // إشعار غرفة الطلبات
        await _realtimeHub.SendToGroupAsync($"order-{orderId}", "OrderStatusChanged", message);
    }

    public async Task NotifyNewOrder(string vendorId, object orderDetails)
    {
        await _realtimeHub.SendToUserAsync(vendorId, "NewOrder", orderDetails);
    }
}
```

### استخدام في العميل (JavaScript)
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/main")
    .build();

// الاستماع للرسائل
connection.on("ReceiveMessage", (method, data) => {
    switch (method) {
        case "OrderStatusChanged":
            updateOrderStatus(data);
            break;
        case "NewMessage":
            showNewMessage(data);
            break;
        case "NewNotification":
            showNotification(data);
            break;
    }
});

await connection.start();
```

### استخدام في العميل (C#)
```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://api.example.com/hubs/main")
    .Build();

connection.On<string, object>("ReceiveMessage", (method, data) =>
{
    Console.WriteLine($"Received: {method} - {data}");
});

await connection.StartAsync();
```

---

## حالات الاستخدام

| الحالة | الطريقة | Method Name |
|--------|---------|-------------|
| إشعار حالة طلب | `SendToUserAsync` | `"OrderStatusChanged"` |
| رسالة محادثة | `SendToGroupAsync` | `"NewMessage"` |
| إشعار عام | `SendToAllAsync` | `"SystemAnnouncement"` |
| تحديث سعر منتج | `SendToGroupAsync` | `"PriceUpdated"` |
| إشعار بائع بطلب جديد | `SendToUserAsync` | `"NewOrder"` |

---

## ملاحظات تقنية

1. **SignalR Ready**: مصمم للتكامل مع ASP.NET Core SignalR
2. **Strongly Typed**: `IRealtimeClient` يوفر typed client
3. **Groups Support**: دعم المجموعات لـ broadcast محدود
4. **Immutable Record**: `RealtimeMessage` كـ record للـ immutability
5. **Abstraction Layer**: يفصل منطق الأعمال عن تفاصيل SignalR
