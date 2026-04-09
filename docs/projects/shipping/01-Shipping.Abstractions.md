# ACommerce.Shipping.Abstractions

## نظرة عامة
تجريدات نظام الشحن. توفر واجهة موحدة لجميع مزودي الشحن مع دعم إنشاء الشحنات والتتبع وحساب التكاليف.

## الموقع
`/Shipping/ACommerce.Shipping.Abstractions`

## التبعيات
- لا توجد تبعيات خارجية (مكتبة تجريدات)

---

## الواجهات (Contracts)

### IShippingProvider
واجهة مزود الشحن:

```csharp
public interface IShippingProvider
{
    // اسم المزود
    string ProviderName { get; }

    // إنشاء شحنة
    Task<ShipmentResult> CreateShipmentAsync(
        ShipmentRequest request,
        CancellationToken cancellationToken = default);

    // تتبع الشحنة
    Task<TrackingInfo> TrackShipmentAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default);

    // إلغاء الشحنة
    Task<bool> CancelShipmentAsync(
        string shipmentId,
        CancellationToken cancellationToken = default);

    // حساب تكلفة الشحن
    Task<decimal> CalculateShippingCostAsync(
        ShipmentRequest request,
        CancellationToken cancellationToken = default);
}
```

---

## النماذج (Models)

### Address
عنوان الشحن:

```csharp
public record Address
{
    public required string FullName { get; init; }
    public required string Phone { get; init; }
    public string? Email { get; init; }
    public required string AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public required string City { get; init; }
    public string? State { get; init; }
    public required string Country { get; init; }
    public required string PostalCode { get; init; }
}
```

### ShipmentRequest
طلب شحن:

```csharp
public record ShipmentRequest
{
    public required string OrderId { get; init; }
    public required Address FromAddress { get; init; }
    public required Address ToAddress { get; init; }
    public required List<Package> Packages { get; init; }
    public string? ServiceType { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
```

### Package
طرد:

```csharp
public record Package
{
    public decimal Weight { get; init; }
    public decimal? Length { get; init; }
    public decimal? Width { get; init; }
    public decimal? Height { get; init; }
    public string? Description { get; init; }
}
```

### ShipmentResult
نتيجة إنشاء شحنة:

```csharp
public record ShipmentResult
{
    public required bool Success { get; init; }
    public required string TrackingNumber { get; init; }
    public required string ShipmentId { get; init; }
    public string? LabelUrl { get; init; }        // رابط بوليصة الشحن
    public decimal? Cost { get; init; }
    public string? ErrorMessage { get; init; }
}
```

### TrackingInfo
معلومات التتبع:

```csharp
public record TrackingInfo
{
    public required string TrackingNumber { get; init; }
    public required ShipmentStatus Status { get; init; }
    public string? CurrentLocation { get; init; }
    public DateTime? EstimatedDelivery { get; init; }
    public List<TrackingEvent> Events { get; init; } = new();
}
```

### TrackingEvent
حدث تتبع:

```csharp
public record TrackingEvent
{
    public required DateTime Timestamp { get; init; }
    public required string Description { get; init; }
    public string? Location { get; init; }
}
```

---

## التعدادات (Enums)

### ShipmentStatus

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `Pending` | 1 | قيد الانتظار |
| `PickedUp` | 2 | تم الاستلام |
| `InTransit` | 3 | في الطريق |
| `OutForDelivery` | 4 | قيد التوصيل |
| `Delivered` | 5 | تم التسليم |
| `Failed` | 6 | فشل التوصيل |
| `Returned` | 7 | مرتجع |
| `Cancelled` | 8 | ملغي |

---

## بنية الملفات
```
ACommerce.Shipping.Abstractions/
├── Contracts/
│   └── IShippingProvider.cs
├── Models/
│   └── ShippingModels.cs    # جميع النماذج
└── Enums/
    └── ShipmentStatus.cs
```

---

## مثال استخدام

### إنشاء شحنة
```csharp
var request = new ShipmentRequest
{
    OrderId = orderId.ToString(),
    FromAddress = new Address
    {
        FullName = "المتجر",
        Phone = "+966501234567",
        AddressLine1 = "شارع الملك فهد",
        City = "الرياض",
        Country = "SA",
        PostalCode = "12345"
    },
    ToAddress = new Address
    {
        FullName = "العميل",
        Phone = "+966501234568",
        AddressLine1 = "شارع التخصصي",
        City = "الرياض",
        Country = "SA",
        PostalCode = "12346"
    },
    Packages = new List<Package>
    {
        new() { Weight = 2.5m, Description = "طلب رقم 123" }
    },
    ServiceType = "express"
};

var result = await shippingProvider.CreateShipmentAsync(request);
```

### تتبع شحنة
```csharp
var tracking = await shippingProvider.TrackShipmentAsync("TRACK123456");

Console.WriteLine($"الحالة: {tracking.Status}");
Console.WriteLine($"الموقع: {tracking.CurrentLocation}");
Console.WriteLine($"التسليم المتوقع: {tracking.EstimatedDelivery}");

foreach (var evt in tracking.Events)
{
    Console.WriteLine($"{evt.Timestamp}: {evt.Description} - {evt.Location}");
}
```

### حساب تكلفة الشحن
```csharp
var cost = await shippingProvider.CalculateShippingCostAsync(request);
Console.WriteLine($"تكلفة الشحن: {cost} ريال");
```

---

## التنفيذات المتاحة
- `ACommerce.Shipping.Mock` - مزود وهمي للاختبار
- يمكن إضافة: Aramex, SMSA, DHL, FedEx, etc.

---

## ملاحظات تقنية

1. **Provider Pattern**: يدعم تبديل مزودي الشحن
2. **Record Types**: استخدام records للـ immutability
3. **Multi-Package**: دعم طرود متعددة
4. **Tracking Events**: سجل كامل لأحداث الشحنة
5. **Label Generation**: دعم إنشاء بوليصة الشحن
