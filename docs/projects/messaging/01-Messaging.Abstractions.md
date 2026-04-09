# ACommerce.Messaging.Abstractions

## نظرة عامة
تجريدات نظام الرسائل (Message Bus). توفر واجهات للنشر والاستهلاك والطلب/الرد.

## الموقع
`/ACommerce.Messaging.Abstractions`

## التبعيات
- لا توجد تبعيات خارجية (مكتبة تجريدات)

---

## الواجهات (Contracts)

### IMessagePublisher
نشر الرسائل:

```csharp
public interface IMessagePublisher
{
    // نشر رسالة واحدة
    Task<MessageResult> PublishAsync<T>(
        T message,
        string topic,
        MessageMetadata? metadata = null,
        CancellationToken cancellationToken = default)
        where T : class;

    // نشر مجموعة رسائل
    Task<MessageResult> PublishBatchAsync<T>(
        IEnumerable<T> messages,
        string topic,
        MessageMetadata? metadata = null,
        CancellationToken cancellationToken = default)
        where T : class;
}
```

### IMessageConsumer
استهلاك الرسائل:

```csharp
public interface IMessageConsumer
{
    // الاشتراك في موضوع
    Task SubscribeAsync<T>(
        string topic,
        Func<T, MessageMetadata, Task<bool>> handler,
        CancellationToken cancellationToken = default)
        where T : class;

    // إلغاء الاشتراك
    Task UnsubscribeAsync(
        string topic,
        CancellationToken cancellationToken = default);
}
```

### IMessageRequestor
الطلب/الرد (Request/Response pattern):

```csharp
public interface IMessageRequestor
{
    Task<TResponse?> RequestAsync<TRequest, TResponse>(
        TRequest request,
        string topic,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;
}
```

### IMessageBus
واجهة شاملة:

```csharp
public interface IMessageBus : IMessagePublisher, IMessageConsumer, IMessageRequestor
{
    bool IsConnected { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
```

---

## النماذج (Models)

### MessageMetadata
بيانات وصفية للرسالة:

```csharp
public class MessageMetadata
{
    public string MessageId { get; set; }
    public string? CorrelationId { get; set; }
    public string? ReplyTo { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Headers { get; set; }
}
```

### MessageResult
نتيجة النشر:

```csharp
public class MessageResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? Error { get; set; }
}
```

### TopicNames
أسماء المواضيع المعرّفة:

```csharp
public static class TopicNames
{
    public const string Orders = "orders";
    public const string Payments = "payments";
    public const string Notifications = "notifications";
    // etc.
}
```

---

## التنفيذات المتاحة
- `ACommerce.Messaging.InMemory` - للاختبار والتطوير
- `ACommerce.Messaging.SignalR` - للاتصال اللحظي

---

## مثال استخدام

### نشر رسالة
```csharp
await messageBus.PublishAsync(new OrderCreatedEvent
{
    OrderId = orderId,
    CustomerId = customerId,
    Total = total
}, TopicNames.Orders);
```

### استهلاك رسائل
```csharp
await messageBus.SubscribeAsync<OrderCreatedEvent>(
    TopicNames.Orders,
    async (message, metadata) =>
    {
        await ProcessOrderAsync(message);
        return true; // acknowledge
    });
```

---

## ملاحظات تقنية

1. **Pub/Sub Pattern**: نشر واشتراك
2. **Request/Response**: طلب/رد
3. **Batch Support**: نشر مجموعات
4. **Metadata**: بيانات وصفية للتتبع
5. **Correlation ID**: ربط الرسائل المرتبطة
