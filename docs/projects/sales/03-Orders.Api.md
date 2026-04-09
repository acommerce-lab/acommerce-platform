# ACommerce.Orders.Api

## نظرة عامة
API للتعامل مع الطلبات. يرث من BaseCrudController ويوفر نقاط نهاية إضافية للعمليات الخاصة.

## الموقع
`/Sales/ACommerce.Orders.Api`

## التبعيات
- `ACommerce.Orders`
- `ACommerce.SharedKernel.AspNetCore`
- `ACommerce.SharedKernel.CQRS`
- `MediatR`

---

## المتحكمات (Controllers)

### OrdersController

```csharp
public class OrdersController : BaseCrudController<Order, CreateOrderDto, CreateOrderDto, OrderResponseDto, CreateOrderDto>
```

يرث عمليات CRUD الأساسية من `BaseCrudController`.

---

## نقاط النهاية (Endpoints)

### عمليات CRUD (موروثة)
| Method | Endpoint | الوصف |
|--------|----------|-------|
| `GET` | `/api/orders` | جلب جميع الطلبات |
| `GET` | `/api/orders/{id}` | جلب طلب بالمعرف |
| `POST` | `/api/orders` | إنشاء طلب جديد |
| `PUT` | `/api/orders/{id}` | تحديث طلب |
| `DELETE` | `/api/orders/{id}` | حذف طلب |

### عمليات مخصصة

#### GET /api/orders/customer/{customerId}
جلب طلبات عميل محدد:

```csharp
[HttpGet("customer/{customerId}")]
public async Task<ActionResult> GetCustomerOrders(string customerId)
```

**استخدام SmartSearch:**
```csharp
var searchRequest = new SmartSearchRequest
{
    PageSize = 50,
    PageNumber = 1,
    Filters = [
        new() { PropertyName = "CustomerId", Value = customerId, Operator = FilterOperator.Equals }
    ],
    OrderBy = "CreatedAt",
    Ascending = false
};
```

#### GET /api/orders/vendor/{vendorId}
جلب طلبات بائع محدد:

```csharp
[HttpGet("vendor/{vendorId}")]
public async Task<ActionResult> GetVendorOrders(Guid vendorId)
```

#### POST /api/orders/{id}/confirm
تأكيد طلب:

```csharp
[HttpPost("{id}/confirm")]
public async Task<IActionResult> ConfirmOrder(Guid id)
```

#### POST /api/orders/{id}/ship
شحن طلب:

```csharp
[HttpPost("{id}/ship")]
public async Task<IActionResult> ShipOrder(Guid id, [FromBody] string trackingNumber)
```

#### POST /api/orders/{id}/cancel
إلغاء طلب:

```csharp
[HttpPost("{id}/cancel")]
public async Task<IActionResult> CancelOrder(Guid id, [FromBody] string? reason)
```

---

## بنية الملفات
```
ACommerce.Orders.Api/
└── Controllers/
    └── OrdersController.cs
```

---

## مثال استخدام

### جلب طلبات العميل
```http
GET /api/orders/customer/user-123
```

**Response:**
```json
{
  "items": [
    {
      "id": "...",
      "orderNumber": "ORD-2024-001",
      "status": "Delivered",
      "total": 299.99
    }
  ],
  "totalCount": 5,
  "pageNumber": 1,
  "pageSize": 50
}
```

### جلب طلبات البائع
```http
GET /api/orders/vendor/550e8400-e29b-41d4-a716-446655440000
```

---

## ملاحظات تقنية

1. **BaseCrudController**: يرث عمليات CRUD الأساسية
2. **SmartSearch**: يستخدم SmartSearch للفلترة والترتيب
3. **MediatR**: يعتمد على CQRS pattern
4. **Vendor/Customer Filtering**: دعم فلترة حسب البائع أو العميل
