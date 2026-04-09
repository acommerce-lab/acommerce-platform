# ACommerce.Authentication.Abstractions

## نظرة عامة
مكتبة تجريدات المصادقة التي تحدد الواجهات الأساسية لمزودي المصادقة (JWT, OpenIddict) والمصادقة الثنائية (2FA: Nafath, SMS, Email).

## الموقع
`/Authentication/ACommerce.Authentication.Abstractions`

## التبعيات
- `ACommerce.SharedKernel.Abstractions`

---

## الواجهات الرئيسية

### IAuthenticationProvider
واجهة مزود المصادقة (JWT, OpenIddict):

```csharp
public interface IAuthenticationProvider
{
    // اسم المزود (مثال: "JWT", "OpenIddict")
    string ProviderName { get; }

    // المصادقة والحصول على tokens
    Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken = default);

    // تجديد Access Token باستخدام Refresh Token
    Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    // التحقق من صحة Token
    Task<TokenValidationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    // إلغاء Token
    Task<bool> RevokeTokenAsync(
        string token,
        CancellationToken cancellationToken = default);
}
```

### ITwoFactorAuthenticationProvider
واجهة المصادقة الثنائية:

```csharp
public interface ITwoFactorAuthenticationProvider
{
    // اسم المزود (مثال: "Nafath", "SMS", "Email")
    string ProviderName { get; }

    // بدء عملية المصادقة الثنائية
    Task<TwoFactorInitiationResult> InitiateAsync(
        TwoFactorInitiationRequest request,
        CancellationToken cancellationToken = default);

    // التحقق من رمز المصادقة
    Task<TwoFactorVerificationResult> VerifyAsync(
        TwoFactorVerificationRequest request,
        CancellationToken cancellationToken = default);

    // إلغاء جلسة المصادقة
    Task<bool> CancelAsync(
        string transactionId,
        CancellationToken cancellationToken = default);

    // تعيين ناشر الأحداث (اختياري)
    void SetEventPublisher(IAuthenticationEventPublisher? publisher);
}
```

### IAuthenticationEventPublisher
ناشر أحداث المصادقة:

```csharp
public interface IAuthenticationEventPublisher
{
    Task PublishAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken = default)
        where TEvent : class, IDomainEvent;
}
```

---

## نماذج الطلب (Request Models)

### AuthenticationRequest
```csharp
public record AuthenticationRequest
{
    public required string Identifier { get; init; }  // البريد/الهاتف/اسم المستخدم
    public string? Credential { get; init; }          // كلمة المرور
    public Dictionary<string, string>? Claims { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
```

### TwoFactorInitiationRequest
```csharp
public record TwoFactorInitiationRequest
{
    public required string Identifier { get; init; }  // رقم الهوية أو الهاتف
    public Dictionary<string, string>? Metadata { get; init; }
}
```

### TwoFactorVerificationRequest
```csharp
public record TwoFactorVerificationRequest
{
    public required string TransactionId { get; init; }
    public string? Code { get; init; }  // رمز التحقق
    public Dictionary<string, string>? Metadata { get; init; }
}
```

---

## نماذج النتيجة (Result Models)

### AuthenticationResult
```csharp
public record AuthenticationResult
{
    public required bool Success { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public string? TokenType { get; init; } = "Bearer";
    public DateTimeOffset? ExpiresAt { get; init; }
    public string? UserId { get; init; }
    public AuthenticationError? Error { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
```

### TwoFactorInitiationResult
```csharp
public record TwoFactorInitiationResult
{
    public required bool Success { get; init; }
    public string? TransactionId { get; init; }
    public string? VerificationCode { get; init; }  // للـ Debug فقط
    public string? Message { get; init; }
    public TimeSpan? ExpiresIn { get; init; }
    public TwoFactorError? Error { get; init; }
    public Dictionary<string, string>? Data { get; init; }

    // Factory methods
    public static TwoFactorInitiationResult Ok(string transactionId, Dictionary<string, string>? data = null);
    public static TwoFactorInitiationResult Fail(TwoFactorError error);
}
```

### TwoFactorVerificationResult
```csharp
public record TwoFactorVerificationResult
{
    public required bool Success { get; init; }
    public string? UserId { get; init; }
    public IReadOnlyDictionary<string, string>? UserClaims { get; init; }
    public TwoFactorError? Error { get; init; }
    public string? TransactionId { get; init; }
    public Dictionary<string, string>? Data { get; init; }

    // Factory methods
    public static TwoFactorVerificationResult Ok(string transactionId, Dictionary<string, string>? data = null);
    public static TwoFactorVerificationResult Fail(TwoFactorError error);
}
```

### TokenValidationResult
```csharp
public record TokenValidationResult
{
    public required bool IsValid { get; init; }
    public string? UserId { get; init; }
    public IReadOnlyDictionary<string, string>? Claims { get; init; }
    public string? Error { get; init; }
}
```

---

## نماذج الأخطاء

### AuthenticationError
```csharp
public record AuthenticationError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? Details { get; init; }
}
```

### TwoFactorError
```csharp
public record TwoFactorError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? Details { get; init; }
}
```

---

## الأحداث (Events)

### UserAuthenticatedEvent
```csharp
public record UserAuthenticatedEvent : IDomainEvent
{
    public required string UserId { get; init; }
    public required string Provider { get; init; }  // "JWT", "Nafath"
    public required string IpAddress { get; init; }
    public string? DeviceInfo { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
```

### أحداث أخرى
- `UserLoggedOutEvent` - تسجيل خروج
- `AuthenticationFailedEvent` - فشل المصادقة

---

## بنية الملفات
```
ACommerce.Authentication.Abstractions/
├── IAuthenticationProvider.cs
├── ITwoFactorAuthenticationProvider.cs
├── AuthenticationRequest.cs
├── AuthenticationResult.cs
├── AuthenticationError.cs
├── AuthenticationException.cs
├── TokenValidationResult.cs
├── TwoFactorInitiationRequest.cs
├── TwoFactorInitiationResult.cs
├── TwoFactorVerificationRequest.cs
├── TwoFactorVerificationResult.cs
├── TwoFactorError.cs
├── TwoFactorAuthenticationException.cs
├── Contracts/
│   ├── IAuthenticationEventPublisher.cs
│   └── IAuthenticationQueryHandler.cs
├── Events/
│   ├── AuthenticationEvents.cs
│   ├── AuthenticationFailedEvent.cs
│   └── UserLoggedOutEvent.cs
└── Queries/
    ├── UserDto.cs
    ├── TokenValidationDto.cs
    ├── ValidateTokenQuery.cs
    └── Queries.cs
```

---

## مثال استخدام

### المصادقة العادية (JWT)
```csharp
var result = await authProvider.AuthenticateAsync(new AuthenticationRequest
{
    Identifier = "user@example.com",
    Credential = "password123"
});

if (result.Success)
{
    var accessToken = result.AccessToken;
    var refreshToken = result.RefreshToken;
}
else
{
    Console.WriteLine($"Error: {result.Error?.Message}");
}
```

### المصادقة الثنائية (نفاذ)
```csharp
// 1. بدء عملية المصادقة
var initResult = await nafathProvider.InitiateAsync(new TwoFactorInitiationRequest
{
    Identifier = "1234567890"  // رقم الهوية
});

if (!initResult.Success)
    return;

var transactionId = initResult.TransactionId;

// 2. التحقق (بعد موافقة المستخدم في تطبيق نفاذ)
var verifyResult = await nafathProvider.VerifyAsync(new TwoFactorVerificationRequest
{
    TransactionId = transactionId
});

if (verifyResult.Success)
{
    var userId = verifyResult.UserId;
    var claims = verifyResult.UserClaims;
}
```

### تجديد Token
```csharp
var result = await authProvider.RefreshAsync(refreshToken);

if (result.Success)
{
    var newAccessToken = result.AccessToken;
}
```

### التحقق من Token
```csharp
var validationResult = await authProvider.ValidateTokenAsync(token);

if (validationResult.IsValid)
{
    var userId = validationResult.UserId;
    var claims = validationResult.Claims;
}
```

---

## ملاحظات تقنية

1. **Provider Pattern**: يمكن تبديل مزودي المصادقة بسهولة
2. **Immutable Records**: جميع النماذج records للـ immutability
3. **Factory Methods**: `Ok()` و `Fail()` لإنشاء النتائج
4. **Event Publishing**: دعم نشر الأحداث للتكامل مع أنظمة أخرى
5. **Metadata Support**: دعم بيانات إضافية في جميع الطلبات والنتائج
