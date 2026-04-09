# ACommerce.Shipping.Api

## نظرة عامة
API للتعامل مع الشحن. يوفر نقاط نهاية لخيارات الشحن وحساب التكاليف والتتبع.

## الموقع
`/Shipping/ACommerce.Shipping.Api`

## التبعيات
- `MediatR`
- `Microsoft.AspNetCore`

---

## المتحكمات (Controllers)

### ShippingController

```csharp
[ApiController]
[Route("api/[controller]")]
public class ShippingController : ControllerBase
```

---

## نقاط النهاية (Endpoints)

### GET /api/shipping/options
الحصول على خيارات الشحن المتاحة:

```csharp
[HttpGet("options")]
public async Task<IActionResult> GetShippingOptions([FromQuery] string? destinationCity)
```

**Response:**
```json
[
  {
    "id": "standard",
    "name": "شحن عادي",
    "price": 25,
    "estimatedDays": "3-5"
  },
  {
    "id": "express",
    "name": "شحن سريع",
    "price": 50,
    "estimatedDays": "1-2"
  }
]
```

### POST /api/shipping/calculate
حساب تكلفة الشحن:

```csharp
[HttpPost("calculate")]
public async Task<IActionResult> CalculateShipping([FromBody] CalculateShippingRequest request)
```

**Request:**
```json
{
  "destinationCity": "الرياض",
  "destinationCountry": "SA",
  "totalWeight": 3.5,
  "itemCount": 2
}
```

**Response:**
```json
{
  "cost": 25,
  "currency": "SAR",
  "estimatedDays": "3-5"
}
```

### GET /api/shipping/track/{trackingNumber}
تتبع شحنة:

```csharp
[HttpGet("track/{trackingNumber}")]
public async Task<IActionResult> TrackShipment(string trackingNumber)
```

**Response:**
```json
{
  "trackingNumber": "MOCK20240101120000",
  "status": "InTransit",
  "events": []
}
```

---

## DTOs

### CalculateShippingRequest

```csharp
public class CalculateShippingRequest
{
    public required string DestinationCity { get; set; }
    public string? DestinationCountry { get; set; } = "SA";
    public decimal TotalWeight { get; set; }
    public int ItemCount { get; set; }
}
```

---

## بنية الملفات
```
ACommerce.Shipping.Api/
└── Controllers/
    └── ShippingController.cs
```

---

## مثال استخدام

### حساب تكلفة الشحن من العميل
```javascript
const response = await fetch('/api/shipping/calculate', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    destinationCity: 'جدة',
    totalWeight: 2.5,
    itemCount: 3
  })
});

const { cost, estimatedDays } = await response.json();
console.log(`تكلفة الشحن: ${cost} ريال - التوصيل خلال ${estimatedDays} أيام`);
```

### تتبع شحنة
```javascript
const tracking = await fetch('/api/shipping/track/MOCK20240101120000');
const { status, events } = await tracking.json();
```

---

## ملاحظات تقنية

1. **MediatR**: يستخدم CQRS pattern
2. **Multiple Options**: دعم خيارات شحن متعددة
3. **Default Country**: SA كدولة افتراضية
4. **Cost Calculation**: حساب التكلفة بناءً على الوزن والوجهة
