# ACommerce.Authentication.TwoFactor.Nafath

## نظرة عامة
تنفيذ مزود المصادقة الثنائية باستخدام خدمة نفاذ (Nafath) السعودية. يدعم تدفق المصادقة الكامل مع Webhooks والأحداث.

## الموقع
`/Authentication/ACommerce.Authentication.TwoFactor.Nafath`

## التبعيات
- `ACommerce.Authentication.Abstractions`
- `ACommerce.Authentication.TwoFactor.Abstractions`

---

## NafathAuthenticationProvider

### الوصف
تنفيذ `ITwoFactorAuthenticationProvider` لخدمة نفاذ:

```csharp
public class NafathAuthenticationProvider : ITwoFactorAuthenticationProvider
{
    public string ProviderName => "Nafath";
}
```

### تدفق المصادقة

```
1. InitiateAsync(nationalId)
   ↓
2. نفاذ يرسل إشعار للمستخدم
   ↓
3. المستخدم يوافق في تطبيق نفاذ
   ↓
4. Webhook يستقبل النتيجة (أو Polling عبر VerifyAsync)
   ↓
5. نجاح/فشل المصادقة
```

---

## NafathOptions

### التكوين
```csharp
public class NafathOptions
{
    public const string SectionName = "Authentication:TwoFactor:Nafath";

    // رابط خدمة نفاذ
    public string BaseUrl { get; set; } = "https://api.authentica.sa/api/v2/";

    // مفتاح API (X-Authorization header)
    public string? ApiKey { get; set; }

    // مفتاح التحقق من Webhook
    public string? WebhookSecret { get; set; }

    // وضع التشغيل
    public NafathMode Mode { get; set; } = NafathMode.Production;

    // رقم هوية للاختبار
    public string TestNationalId { get; set; } = "2507643761";

    // مدة صلاحية الجلسة (دقائق)
    public int SessionExpirationMinutes { get; set; } = 5;
}
```

### NafathMode
```csharp
public enum NafathMode
{
    Production,  // الإنتاج - يتصل بخدمة نفاذ الحقيقية
    Test         // الاختبار - يستخدم رقم هوية ثابت للتجربة
}
```

### مثال appsettings.json
```json
{
  "Authentication": {
    "TwoFactor": {
      "Nafath": {
        "BaseUrl": "https://api.authentica.sa/api/v2/",
        "ApiKey": "your-nafath-api-key",
        "WebhookSecret": "your-webhook-secret",
        "Mode": "Production",
        "SessionExpirationMinutes": 5
      }
    }
  }
}
```

---

## INafathApiClient

### الواجهة
```csharp
public interface INafathApiClient
{
    // بدء المصادقة
    Task<NafathInitiationResponse> InitiateAuthenticationAsync(
        string nationalId,
        CancellationToken cancellationToken = default);

    // التحقق من الحالة (Polling)
    Task<NafathStatusResponse> CheckStatusAsync(
        string transactionId,
        CancellationToken cancellationToken = default);
}
```

### NafathInitiationResponse
```csharp
public record NafathInitiationResponse
{
    public bool Success { get; init; }
    public string? TransactionId { get; init; }
    public string? VerificationCode { get; init; }  // الكود المعروض للمستخدم
    public TwoFactorError? Error { get; init; }
}
```

### NafathStatusResponse
```csharp
public record NafathStatusResponse
{
    public bool IsCompleted { get; init; }
    public string? Status { get; init; }  // "PENDING", "COMPLETED", "FAILED"
}
```

---

## Webhook

### NafathWebhookRequest
```csharp
public record NafathWebhookRequest
{
    public required string TransactionId { get; init; }
    public required string NationalId { get; init; }
    public required string Status { get; init; }  // "COMPLETED", "FAILED"
    public required string Password { get; init; }
}
```

### معالجة Webhook
```csharp
public async Task<bool> HandleWebhookAsync(
    NafathWebhookRequest request,
    CancellationToken cancellationToken = default)
```

**ما يفعله:**
1. يجد الجلسة بـ `TransactionId`
2. يحدث الحالة (`Verified` أو `Failed`)
3. ينشر الأحداث (`TwoFactorSucceededEvent` أو `TwoFactorFailedEvent`)

---

## مثال استخدام

### بدء المصادقة
```csharp
var initResult = await nafathProvider.InitiateAsync(new TwoFactorInitiationRequest
{
    Identifier = "1234567890"  // رقم الهوية الوطنية
});

if (initResult.Success)
{
    var transactionId = initResult.TransactionId;
    var verificationCode = initResult.Data?["verificationCode"];

    // اعرض verificationCode للمستخدم ليتأكد منه في تطبيق نفاذ
    Console.WriteLine($"رجاء تأكيد الرقم {verificationCode} في تطبيق نفاذ");
}
```

### التحقق (Polling)
```csharp
// يُستخدم إذا لم يكن Webhook متاحاً
var verifyResult = await nafathProvider.VerifyAsync(new TwoFactorVerificationRequest
{
    TransactionId = transactionId
});

if (verifyResult.Success)
{
    // تم التحقق بنجاح - أكمل تسجيل الدخول
}
else if (verifyResult.Error?.Code == "VERIFICATION_PENDING")
{
    // المستخدم لم يوافق بعد - حاول مرة أخرى
}
```

### Webhook Controller
```csharp
[ApiController]
[Route("api/webhooks/nafath")]
public class NafathWebhookController : ControllerBase
{
    private readonly NafathAuthenticationProvider _nafathProvider;

    [HttpPost]
    public async Task<IActionResult> HandleWebhook([FromBody] NafathWebhookRequest request)
    {
        var result = await _nafathProvider.HandleWebhookAsync(request);

        return result ? Ok() : BadRequest();
    }
}
```

---

## الأحداث

### TwoFactorSucceededEvent
يُنشر عند نجاح المصادقة:
```csharp
new TwoFactorSucceededEvent
{
    TransactionId = "xxx",
    Identifier = "1234567890",
    Provider = "Nafath"
}
```

### TwoFactorFailedEvent
يُنشر عند فشل المصادقة:
```csharp
new TwoFactorFailedEvent
{
    TransactionId = "xxx",
    Identifier = "1234567890",
    Provider = "Nafath",
    Reason = "USER_REJECTED"
}
```

---

## بنية الملفات
```
ACommerce.Authentication.TwoFactor.Nafath/
├── NafathAuthenticationProvider.cs
├── NafathOptions.cs
├── NafathMode.cs
├── INafathApiClient.cs
├── NafathApiClient.cs
├── NafathWebhookRequest.cs
└── ServiceCollectionExtensions.cs
```

---

## تسجيل الخدمات

```csharp
// في Program.cs
builder.Services.AddNafathAuthentication(builder.Configuration);
```

---

## ملاحظات تقنية

1. **Session Store**: يتطلب `ITwoFactorSessionStore` لحفظ الجلسات
2. **Webhook vs Polling**: يُفضل استخدام Webhook للأداء الأفضل
3. **Verification Code**: يُعرض للمستخدم للتأكد من صحة الطلب
4. **Expiration**: الجلسات تنتهي بعد 5 دقائق افتراضياً
5. **Event Publishing**: اختياري - يتطلب `IAuthenticationEventPublisher`
