# ACommerce.Payments.Abstractions

## نظرة عامة
تجريدات نظام الدفع. توفر واجهة موحدة لجميع مزودي الدفع مع دعم العمليات الأساسية والاسترجاع.

## الموقع
`/Payments/ACommerce.Payments.Abstractions`

## التبعيات
- لا توجد تبعيات خارجية (مكتبة تجريدات)

---

## الواجهات (Contracts)

### IPaymentProvider
واجهة مزود الدفع:

```csharp
public interface IPaymentProvider
{
    // اسم المزود
    string ProviderName { get; }

    // إنشاء عملية دفع
    Task<PaymentResult> CreatePaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default);

    // التحقق من حالة الدفع
    Task<PaymentResult> GetPaymentStatusAsync(
        string transactionId,
        CancellationToken cancellationToken = default);

    // استرجاع مبلغ
    Task<RefundResult> RefundAsync(
        RefundRequest request,
        CancellationToken cancellationToken = default);

    // إلغاء عملية دفع
    Task<bool> CancelPaymentAsync(
        string transactionId,
        CancellationToken cancellationToken = default);

    // التحقق من webhook
    Task<bool> ValidateWebhookAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken = default);
}
```

---

## النماذج (Models)

### PaymentRequest
طلب دفع:

```csharp
public record PaymentRequest
{
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string OrderId { get; init; }
    public required string CustomerId { get; init; }
    public PaymentMethod? Method { get; init; }
    public string? CallbackUrl { get; init; }
    public string? Description { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
```

### PaymentResult
نتيجة الدفع:

```csharp
public record PaymentResult
{
    public required bool Success { get; init; }
    public required string TransactionId { get; init; }
    public required PaymentStatus Status { get; init; }
    public string? PaymentUrl { get; init; }     // رابط صفحة الدفع
    public string? ErrorMessage { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
```

### RefundRequest
طلب استرجاع:

```csharp
public record RefundRequest
{
    public required string TransactionId { get; init; }
    public required decimal Amount { get; init; }
    public string? Reason { get; init; }
}
```

### RefundResult
نتيجة الاسترجاع:

```csharp
public record RefundResult
{
    public required bool Success { get; init; }
    public required string RefundId { get; init; }
    public string? ErrorMessage { get; init; }
}
```

---

## التعدادات (Enums)

### PaymentStatus

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `Pending` | 1 | قيد الانتظار |
| `Processing` | 2 | قيد المعالجة |
| `Completed` | 3 | مكتمل |
| `Failed` | 4 | فاشل |
| `Cancelled` | 5 | ملغي |
| `Refunded` | 6 | مسترجع |
| `PartiallyRefunded` | 7 | مسترجع جزئياً |

### PaymentMethod

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `CreditCard` | 1 | بطاقة ائتمان |
| `DebitCard` | 2 | بطاقة خصم |
| `BankTransfer` | 3 | تحويل بنكي |
| `Wallet` | 4 | محفظة إلكترونية |
| `CashOnDelivery` | 5 | الدفع عند الاستلام |
| `ApplePay` | 6 | Apple Pay |
| `GooglePay` | 7 | Google Pay |
| `Tabby` | 8 | تابي (اشتر الآن وادفع لاحقاً) |
| `Tamara` | 9 | تمارا (اشتر الآن وادفع لاحقاً) |

---

## بنية الملفات
```
ACommerce.Payments.Abstractions/
├── Contracts/
│   └── IPaymentProvider.cs
├── Models/
│   └── PaymentRequest.cs   # جميع النماذج
└── Enums/
    └── PaymentStatus.cs    # PaymentStatus + PaymentMethod
```

---

## مثال استخدام

### إنشاء عملية دفع
```csharp
public class CheckoutService
{
    private readonly IPaymentProvider _paymentProvider;

    public async Task<PaymentResult> ProcessPaymentAsync(Order order)
    {
        var request = new PaymentRequest
        {
            Amount = order.Total,
            Currency = order.Currency,
            OrderId = order.Id.ToString(),
            CustomerId = order.CustomerId,
            Method = PaymentMethod.CreditCard,
            CallbackUrl = "https://example.com/payment/callback",
            Description = $"Order #{order.OrderNumber}"
        };

        return await _paymentProvider.CreatePaymentAsync(request);
    }
}
```

### التحقق من حالة الدفع
```csharp
var result = await _paymentProvider.GetPaymentStatusAsync(transactionId);

if (result.Status == PaymentStatus.Completed)
{
    await ConfirmOrderAsync(orderId);
}
```

### استرجاع مبلغ
```csharp
var refundResult = await _paymentProvider.RefundAsync(new RefundRequest
{
    TransactionId = paymentId,
    Amount = 100.00m,
    Reason = "طلب العميل"
});
```

---

## التنفيذات المتاحة
- `ACommerce.Payments.Moyasar` - بوابة دفع Moyasar

---

## ملاحظات تقنية

1. **Provider Pattern**: يدعم تبديل مزودي الدفع
2. **Record Types**: استخدام records للـ immutability
3. **BNPL Support**: دعم Tabby و Tamara
4. **Webhook Validation**: التحقق من صحة Webhooks
5. **Partial Refund**: دعم الاسترجاع الجزئي
