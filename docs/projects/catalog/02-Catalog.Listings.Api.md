# ACommerce.Catalog.Listings.Api

## نظرة عامة
API للتعامل مع عروض المنتجات. يرث من BaseCrudController ويوفر نقاط نهاية للبحث حسب المنتج أو البائع.

## الموقع
`/Catalog/ACommerce.Catalog.Listings.Api`

## التبعيات
- `ACommerce.Catalog.Listings`
- `ACommerce.SharedKernel.AspNetCore`
- `ACommerce.SharedKernel.CQRS`
- `MediatR`

---

## المتحكمات (Controllers)

### ProductListingsController

```csharp
public class ProductListingsController : BaseCrudController<ProductListing, CreateListingDto, CreateListingDto, ListingResponseDto, CreateListingDto>
```

يرث عمليات CRUD الأساسية من `BaseCrudController`.

---

## نقاط النهاية (Endpoints)

### عمليات CRUD (موروثة)
| Method | Endpoint | الوصف |
|--------|----------|-------|
| `GET` | `/api/productlistings` | جلب جميع العروض |
| `GET` | `/api/productlistings/{id}` | جلب عرض بالمعرف |
| `POST` | `/api/productlistings` | إنشاء عرض جديد |
| `PUT` | `/api/productlistings/{id}` | تحديث عرض |
| `DELETE` | `/api/productlistings/{id}` | حذف عرض |

### عمليات مخصصة

#### GET /api/productlistings/by-product/{productId}
جلب جميع عروض منتج معين:

```csharp
[HttpGet("by-product/{productId}")]
public async Task<ActionResult> GetByProduct(Guid productId, [FromQuery] bool activeOnly = true)
```

**Parameters:**
- `productId`: معرف المنتج
- `activeOnly`: فلترة العروض النشطة فقط (افتراضي: true)

**استخدام SmartSearch:**
```csharp
var searchRequest = new SmartSearchRequest
{
    PageSize = 100,
    PageNumber = 1,
    Filters = [
        new() { PropertyName = "ProductId", Value = productId.ToString(), Operator = FilterOperator.Equals }
    ]
};

if (activeOnly)
{
    searchRequest.Filters.Add(new() { PropertyName = "IsActive", Value = "true", Operator = FilterOperator.Equals });
}
```

#### GET /api/productlistings/by-vendor/{vendorId}
جلب جميع عروض بائع معين:

```csharp
[HttpGet("by-vendor/{vendorId}")]
public async Task<ActionResult> GetByVendor(Guid vendorId)
```

---

## بنية الملفات
```
ACommerce.Catalog.Listings.Api/
└── Controllers/
    └── ProductListingsController.cs
```

---

## مثال استخدام

### جلب عروض منتج
```http
GET /api/productlistings/by-product/550e8400-e29b-41d4-a716-446655440000?activeOnly=true
```

**Response:**
```json
{
  "items": [
    {
      "id": "...",
      "vendorId": "...",
      "productId": "550e8400-e29b-41d4-a716-446655440000",
      "price": 2999.00,
      "quantityAvailable": 15,
      "isActive": true
    }
  ],
  "totalCount": 3,
  "pageNumber": 1,
  "pageSize": 100
}
```

### جلب عروض بائع
```http
GET /api/productlistings/by-vendor/660e8400-e29b-41d4-a716-446655440001
```

---

## ملاحظات تقنية

1. **BaseCrudController**: يرث عمليات CRUD الأساسية
2. **SmartSearch**: يستخدم SmartSearch للفلترة
3. **MediatR**: يعتمد على CQRS pattern
4. **Multi-Vendor**: يدعم البحث حسب البائع أو المنتج
5. **Active Filter**: فلتر اختياري للعروض النشطة
