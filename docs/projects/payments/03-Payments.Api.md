# ACommerce.Payments.Api

## نظرة عامة
API للتعامل مع عمليات الدفع. يوفر نقاط نهاية لإنشاء الدفعات واستقبال Webhooks.

## الموقع
`/Payments/ACommerce.Payments.Api`

## التبعيات
- `MediatR`
- `Microsoft.AspNetCore`

---

## المتحكمات (Controllers)

### PaymentsController

```csharp
[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
```

---

## نقاط النهاية (Endpoints)

### POST /api/payments
إنشاء عملية دفع جديدة:

```csharp
[HttpPost]
public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
```

**Request:**
```json
{
  "orderId": "order-123",
  "amount": 299.99,
  "currency": "SAR",
  "paymentMethod": "CreditCard"
}
```

**Response:**
```json
{
  "paymentId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Pending"
}
```

### GET /api/payments/{id}
الحصول على حالة عملية دفع:

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetPaymentStatus(Guid id)
```

**Response:**
```json
{
  "paymentId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Completed"
}
```

### POST /api/payments/webhook
استقبال تحديثات من بوابة الدفع:

```csharp
[HttpPost("webhook")]
public async Task<IActionResult> Webhook([FromBody] object payload)
```

---

## DTOs

### CreatePaymentRequest

```csharp
public class CreatePaymentRequest
{
    public required string OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SAR";
    public string? PaymentMethod { get; set; }
}
```

---

## بنية الملفات
```
ACommerce.Payments.Api/
└── Controllers/
    └── PaymentsController.cs
```

---

## مثال استخدام

### إنشاء دفعة من العميل
```javascript
const response = await fetch('/api/payments', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    orderId: 'order-123',
    amount: 299.99,
    currency: 'SAR',
    paymentMethod: 'CreditCard'
  })
});

const { paymentId, paymentUrl } = await response.json();
// توجيه للدفع
window.location.href = paymentUrl;
```

### معالجة Webhook
```csharp
[HttpPost("webhook")]
public async Task<IActionResult> Webhook([FromBody] object payload)
{
    // التحقق من التوقيع
    var signature = Request.Headers["X-Signature"].ToString();

    if (!await _paymentProvider.ValidateWebhookAsync(payload.ToString(), signature))
    {
        return Unauthorized();
    }

    // معالجة الحدث
    // تحديث حالة الطلب

    return Ok();
}
```

---

## ملاحظات تقنية

1. **MediatR**: يستخدم CQRS pattern
2. **Webhook Support**: نقطة نهاية لاستقبال الـ webhooks
3. **Default Currency**: SAR كعملة افتراضية
4. **Status Tracking**: تتبع حالة الدفعات
