# ACommerce.Authentication.TwoFactor.Abstractions

## نظرة عامة
تجريدات المصادقة الثنائية (2FA). توفر واجهة مخزن الجلسات والأحداث المشتركة بين جميع مزودي 2FA.

## الموقع
`/Authentication/ACommerce.Authentication.TwoFactor.Abstractions`

## التبعيات
- `ACommerce.SharedKernel.Abstractions`

---

## ITwoFactorSessionStore

### الواجهة
مخزن جلسات المصادقة الثنائية:

```csharp
public interface ITwoFactorSessionStore
{
    // إنشاء جلسة جديدة
    Task<string> CreateSessionAsync(
        TwoFactorSession session,
        CancellationToken cancellationToken = default);

    // الحصول على جلسة
    Task<TwoFactorSession?> GetSessionAsync(
        string transactionId,
        CancellationToken cancellationToken = default);

    // تحديث جلسة
    Task UpdateSessionAsync(
        TwoFactorSession session,
        CancellationToken cancellationToken = default);

    // حذف جلسة
    Task DeleteSessionAsync(
        string transactionId,
        CancellationToken cancellationToken = default);
}
```

### التنفيذات المتاحة
- `InMemoryTwoFactorSessionStore` - للتطوير والاختبار
- `EntityFrameworkTwoFactorSessionStore` - للإنتاج مع قاعدة بيانات

---

## TwoFactorSession

### النموذج
```csharp
public record TwoFactorSession
{
    public required string TransactionId { get; init; }
    public required string Identifier { get; init; }    // رقم الهوية/الهاتف
    public required string Provider { get; init; }      // "Nafath", "SMS", "Email"
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public string? VerificationCode { get; init; }      // رمز التحقق
    public TwoFactorSessionStatus Status { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
```

### TwoFactorSessionStatus
```csharp
public enum TwoFactorSessionStatus
{
    Pending,    // في انتظار التحقق
    Verified,   // تم التحقق بنجاح
    Expired,    // انتهت الصلاحية
    Cancelled,  // تم الإلغاء
    Failed      // فشل التحقق
}
```

---

## الأحداث (Events)

### TwoFactorSucceededEvent
```csharp
public record TwoFactorSucceededEvent : IDomainEvent
{
    public required string TransactionId { get; init; }
    public required string Identifier { get; init; }
    public required string Provider { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
```

### TwoFactorFailedEvent
```csharp
public record TwoFactorFailedEvent : IDomainEvent
{
    public required string TransactionId { get; init; }
    public required string Identifier { get; init; }
    public required string Provider { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
```

### TwoFactorInitiatedEvent
يُنشر عند بدء عملية المصادقة.

### TwoFactorExpiredEvent
يُنشر عند انتهاء صلاحية الجلسة.

---

## بنية الملفات
```
ACommerce.Authentication.TwoFactor.Abstractions/
├── ITwoFactorSessionStore.cs   # الواجهة + TwoFactorSession + Status
└── Events/
    ├── TwoFactorSucceededEvent.cs
    ├── TwoFactorFailedEvent.cs
    ├── TwoFactorInitiatedEvent.cs
    └── TwoFactorExpiredEvent.cs
```

---

## مثال استخدام

### تنفيذ InMemory (للتجربة)
```csharp
public class InMemoryTwoFactorSessionStore : ITwoFactorSessionStore
{
    private readonly ConcurrentDictionary<string, TwoFactorSession> _sessions = new();

    public Task<string> CreateSessionAsync(TwoFactorSession session, CancellationToken ct = default)
    {
        _sessions[session.TransactionId] = session;
        return Task.FromResult(session.TransactionId);
    }

    public Task<TwoFactorSession?> GetSessionAsync(string transactionId, CancellationToken ct = default)
    {
        _sessions.TryGetValue(transactionId, out var session);
        return Task.FromResult(session);
    }

    public Task UpdateSessionAsync(TwoFactorSession session, CancellationToken ct = default)
    {
        _sessions[session.TransactionId] = session;
        return Task.CompletedTask;
    }

    public Task DeleteSessionAsync(string transactionId, CancellationToken ct = default)
    {
        _sessions.TryRemove(transactionId, out _);
        return Task.CompletedTask;
    }
}
```

### معالجة الأحداث
```csharp
public class TwoFactorEventHandler :
    INotificationHandler<TwoFactorSucceededEvent>,
    INotificationHandler<TwoFactorFailedEvent>
{
    public async Task Handle(TwoFactorSucceededEvent notification, CancellationToken ct)
    {
        // تسجيل الدخول للمستخدم
        // إرسال إشعار نجاح
    }

    public async Task Handle(TwoFactorFailedEvent notification, CancellationToken ct)
    {
        // تسجيل المحاولة الفاشلة
        // تحذير أمني إذا تكررت المحاولات
    }
}
```

---

## ملاحظات تقنية

1. **Record Type**: `TwoFactorSession` هو record للـ immutability
2. **IDomainEvent**: الأحداث تنفذ `IDomainEvent` للتكامل مع MediatR
3. **Provider Agnostic**: يعمل مع أي مزود 2FA
4. **Expiration**: دعم انتهاء صلاحية الجلسات
