# ACommerce.Payments.Moyasar

## نظرة عامة
تنفيذ مزود الدفع Moyasar. بوابة دفع سعودية تدعم البطاقات والتحويلات البنكية.

## الموقع
`/Payments/ACommerce.Payments.Moyasar`

## التبعيات
- `ACommerce.Payments.Abstractions`
- `Microsoft.Extensions.Options`

---

## الخدمات (Services)

### MoyasarPaymentProvider

```csharp
public class MoyasarPaymentProvider : IPaymentProvider
{
    public string ProviderName => "Moyasar";
}
```

---

## الخيارات (Options)

### MoyasarOptions

```csharp
public class MoyasarOptions
{
    public required string ApiKey { get; set; }
    public required string PublishableKey { get; set; }
    public string ApiUrl { get; set; } = "https://api.moyasar.com/v1";
    public bool UseSandbox { get; set; }
}
```

| الخاصية | النوع | الوصف |
|---------|------|-------|
| `ApiKey` | `string` | مفتاح API السري |
| `PublishableKey` | `string` | المفتاح العام (للفورم) |
| `ApiUrl` | `string` | عنوان API |
| `UseSandbox` | `bool` | استخدام بيئة الاختبار |

---

## التنفيذات

### CreatePaymentAsync
إنشاء عملية دفع:

```csharp
public async Task<PaymentResult> CreatePaymentAsync(
    PaymentRequest request,
    CancellationToken cancellationToken = default)
{
    var payload = new
    {
        amount = (int)(request.Amount * 100), // تحويل لهللات
        currency = request.Currency.ToUpper(),
        description = request.Description ?? $"Order {request.OrderId}",
        callback_url = request.CallbackUrl,
        metadata = request.Metadata
    };

    var response = await client.PostAsJsonAsync($"{_options.ApiUrl}/payments", payload);
    // ...
}
```

**ملاحظة:** المبلغ يُحوَّل إلى هللات (× 100).

### GetPaymentStatusAsync
التحقق من حالة الدفع:

```csharp
public async Task<PaymentResult> GetPaymentStatusAsync(
    string transactionId,
    CancellationToken cancellationToken = default)
{
    var response = await client.GetAsync($"{_options.ApiUrl}/payments/{transactionId}");
    // ...
}
```

**تحويل الحالات:**
| Moyasar Status | PaymentStatus |
|----------------|---------------|
| `paid` | `Completed` |
| `failed` | `Failed` |
| `refunded` | `Refunded` |
| أخرى | `Pending` |

### RefundAsync
استرجاع مبلغ:

```csharp
public async Task<RefundResult> RefundAsync(
    RefundRequest request,
    CancellationToken cancellationToken = default)
{
    var payload = new { amount = (int)(request.Amount * 100) };
    var response = await client.PostAsJsonAsync(
        $"{_options.ApiUrl}/payments/{request.TransactionId}/refund",
        payload);
    // ...
}
```

### CancelPaymentAsync
إلغاء عملية دفع:

```csharp
public Task<bool> CancelPaymentAsync(string transactionId, CancellationToken ct = default)
{
    // Moyasar لا تدعم الإلغاء
    return Task.FromResult(false);
}
```

---

## بنية الملفات
```
ACommerce.Payments.Moyasar/
├── Services/
│   └── MoyasarPaymentProvider.cs
└── Models/
    └── MoyasarOptions.cs
```

---

## الإعدادات

### appsettings.json
```json
{
  "Moyasar": {
    "ApiKey": "sk_test_xxxxxxxx",
    "PublishableKey": "pk_test_xxxxxxxx",
    "ApiUrl": "https://api.moyasar.com/v1",
    "UseSandbox": true
  }
}
```

### تسجيل الخدمة
```csharp
services.Configure<MoyasarOptions>(configuration.GetSection("Moyasar"));
services.AddHttpClient();
services.AddScoped<IPaymentProvider, MoyasarPaymentProvider>();
```

---

## مثال استخدام

### إنشاء دفعة
```csharp
var provider = new MoyasarPaymentProvider(options, httpClientFactory);

var result = await provider.CreatePaymentAsync(new PaymentRequest
{
    Amount = 150.00m,
    Currency = "SAR",
    OrderId = orderId.ToString(),
    CustomerId = customerId,
    CallbackUrl = "https://mystore.com/payment/callback",
    Description = "طلب من متجر XYZ"
});

if (result.Success)
{
    // توجيه العميل لصفحة الدفع
    return Redirect(result.PaymentUrl);
}
```

### التحقق من الدفع (Callback)
```csharp
public async Task<IActionResult> PaymentCallback(string id)
{
    var result = await _paymentProvider.GetPaymentStatusAsync(id);

    if (result.Status == PaymentStatus.Completed)
    {
        await _orderService.ConfirmOrderAsync(orderId);
        return RedirectToAction("Success");
    }

    return RedirectToAction("Failed");
}
```

---

## ملاحظات تقنية

1. **Amount Conversion**: المبالغ بالهللات (SAR × 100)
2. **Basic Auth**: يستخدم Basic Authentication
3. **No Cancel**: Moyasar لا تدعم إلغاء العمليات
4. **Sandbox**: دعم بيئة الاختبار
5. **IHttpClientFactory**: يستخدم HttpClientFactory للاتصالات
