# ACommerce.Shipping.Mock

## نظرة عامة
مزود شحن وهمي للاختبار والتطوير. يحاكي سلوك مزودي الشحن الحقيقيين مع حسابات بسيطة للتكلفة.

## الموقع
`/Shipping/ACommerce.Shipping.Mock`

## التبعيات
- `ACommerce.Shipping.Abstractions`

---

## الخدمات (Services)

### MockShippingProvider

```csharp
public class MockShippingProvider : IShippingProvider
{
    public string ProviderName => "Mock Shipping";
}
```

---

## التنفيذات

### CreateShipmentAsync
إنشاء شحنة:

```csharp
public Task<ShipmentResult> CreateShipmentAsync(
    ShipmentRequest request,
    CancellationToken cancellationToken = default)
{
    var trackingNumber = $"MOCK{DateTime.UtcNow:yyyyMMddHHmmss}";

    return Task.FromResult(new ShipmentResult
    {
        Success = true,
        TrackingNumber = trackingNumber,
        ShipmentId = Guid.NewGuid().ToString(),
        LabelUrl = $"https://example.com/labels/{trackingNumber}",
        Cost = CalculateCost(request)
    });
}
```

**رقم التتبع:** `MOCK` + timestamp

### TrackShipmentAsync
تتبع شحنة:

```csharp
public Task<TrackingInfo> TrackShipmentAsync(
    string trackingNumber,
    CancellationToken cancellationToken = default)
{
    return Task.FromResult(new TrackingInfo
    {
        TrackingNumber = trackingNumber,
        Status = ShipmentStatus.InTransit,
        CurrentLocation = "Distribution Center",
        EstimatedDelivery = DateTime.UtcNow.AddDays(2),
        Events = new List<TrackingEvent>
        {
            new() { Timestamp = DateTime.UtcNow.AddHours(-2), Description = "Picked up", Location = "Sender Address" },
            new() { Timestamp = DateTime.UtcNow.AddHours(-1), Description = "In transit", Location = "Distribution Center" }
        }
    });
}
```

### CancelShipmentAsync
إلغاء شحنة:

```csharp
public Task<bool> CancelShipmentAsync(
    string shipmentId,
    CancellationToken cancellationToken = default)
{
    return Task.FromResult(true);
}
```

### CalculateShippingCostAsync
حساب تكلفة الشحن:

```csharp
public Task<decimal> CalculateShippingCostAsync(
    ShipmentRequest request,
    CancellationToken cancellationToken = default)
{
    return Task.FromResult(CalculateCost(request));
}

private static decimal CalculateCost(ShipmentRequest request)
{
    var totalWeight = request.Packages.Sum(p => p.Weight);
    var baseCost = 20m;              // 20 ريال أساسي
    var weightCost = totalWeight * 2m; // 2 ريال لكل كيلو
    return baseCost + weightCost;
}
```

**معادلة التكلفة:** `20 + (الوزن × 2)` ريال

---

## بنية الملفات
```
ACommerce.Shipping.Mock/
└── Services/
    └── MockShippingProvider.cs
```

---

## مثال استخدام

### للاختبار
```csharp
services.AddScoped<IShippingProvider, MockShippingProvider>();
```

### اختبار إنشاء شحنة
```csharp
var provider = new MockShippingProvider();

var result = await provider.CreateShipmentAsync(new ShipmentRequest
{
    OrderId = "order-123",
    FromAddress = vendorAddress,
    ToAddress = customerAddress,
    Packages = new List<Package>
    {
        new() { Weight = 5m }  // 5 كيلو
    }
});

// Cost = 20 + (5 × 2) = 30 ريال
Assert.Equal(30m, result.Cost);
```

---

## ملاحظات تقنية

1. **Development Only**: للاختبار والتطوير فقط
2. **Simple Pricing**: تسعير بسيط بناءً على الوزن
3. **Mock Data**: يعيد بيانات محاكاة
4. **Always Success**: ينجح دائماً (لا أخطاء)
5. **Replaceable**: يمكن استبداله بـ Aramex, SMSA, DHL
