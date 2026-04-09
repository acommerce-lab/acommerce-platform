# ACommerce.Catalog.Listings

## نظرة عامة
مكتبة عروض المنتجات للبائعين (Multi-Vendor Listings). تربط البائع بالمنتج مع سعر ومخزون خاص به.

## الموقع
`/Catalog/ACommerce.Catalog.Listings`

## التبعيات
- `ACommerce.SharedKernel.Abstractions`

---

## المفهوم الأساسي

في نظام Multi-Vendor، المنتج الواحد يمكن أن يُعرض من عدة بائعين:

```
Product (هاتف Samsung S24)
├── Listing 1: بائع A - السعر 3000 ريال - متوفر 10
├── Listing 2: بائع B - السعر 2900 ريال - متوفر 5
└── Listing 3: بائع C - السعر 3100 ريال - متوفر 20
```

العميل يختار من أي بائع يريد الشراء بناءً على السعر، التوفر، والتقييم.

---

## الكيانات (Entities)

### ProductListing
عرض المنتج من البائع:

```csharp
public class ProductListing : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // الربط
    public Guid VendorId { get; set; }
    public Guid ProductId { get; set; }
    public string? VendorSku { get; set; }

    // الحالة
    public ListingStatus Status { get; set; } = ListingStatus.Draft;
    public bool IsActive { get; set; } = true;

    // التسعير
    public required decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }  // سعر المقارنة
    public decimal? Cost { get; set; }            // التكلفة
    public Guid? CurrencyId { get; set; }

    // المخزون
    public int QuantityAvailable { get; set; }
    public int QuantityReserved { get; set; }
    public int? LowStockThreshold { get; set; }

    // التوقيت
    public int? ProcessingTime { get; set; }  // أيام
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    // الإحصائيات
    public int TotalSales { get; set; }
    public int ViewCount { get; set; }
    public decimal? Rating { get; set; }
    public int ReviewCount { get; set; }

    // إضافي
    public string? VendorNotes { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
}
```

### خصائص مهمة

| الخاصية | النوع | الوصف |
|---------|------|-------|
| `VendorId` | `Guid` | معرف البائع |
| `ProductId` | `Guid` | معرف المنتج الأساسي |
| `VendorSku` | `string?` | SKU خاص بالبائع |
| `Price` | `decimal` | السعر الحالي |
| `CompareAtPrice` | `decimal?` | السعر قبل الخصم |
| `QuantityAvailable` | `int` | الكمية المتوفرة |
| `QuantityReserved` | `int` | الكمية المحجوزة (في سلال) |
| `ProcessingTime` | `int?` | وقت التحضير بالأيام |

---

## التعدادات (Enums)

### ListingStatus

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `Draft` | 1 | مسودة - لم يُنشر بعد |
| `PendingReview` | 2 | قيد المراجعة من الإدارة |
| `Active` | 3 | نشط - معروض للعملاء |
| `OutOfStock` | 4 | نفذت الكمية |
| `Suspended` | 5 | معلق من الإدارة |
| `Rejected` | 6 | مرفوض |

---

## DTOs

### CreateListingDto
```csharp
public class CreateListingDto
{
    public Guid VendorId { get; set; }
    public Guid ProductId { get; set; }
    public string? VendorSku { get; set; }
    public required decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public decimal? Cost { get; set; }
    public Guid? CurrencyId { get; set; }
    public int QuantityAvailable { get; set; }
    public int? ProcessingTime { get; set; }
    public string? VendorNotes { get; set; }
}
```

### ListingResponseDto
```csharp
public class ListingResponseDto
{
    public Guid Id { get; set; }
    public Guid VendorId { get; set; }
    public Guid ProductId { get; set; }
    public string? VendorSku { get; set; }
    public ListingStatus Status { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int QuantityAvailable { get; set; }
    public int QuantityReserved { get; set; }
    public bool IsActive { get; set; }
    public int TotalSales { get; set; }
    public decimal? Rating { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

## بنية الملفات
```
ACommerce.Catalog.Listings/
├── Entities/
│   └── ProductListing.cs
├── DTOs/
│   ├── CreateListingDto.cs
│   └── ListingResponseDto.cs
└── Enums/
    └── ListingStatus.cs
```

---

## مثال استخدام

### إنشاء عرض جديد
```csharp
var listing = new ProductListing
{
    VendorId = vendorId,
    ProductId = productId,
    VendorSku = "VENDOR-SKU-001",
    Price = 2999.00m,
    CompareAtPrice = 3499.00m,
    QuantityAvailable = 50,
    ProcessingTime = 2,
    Status = ListingStatus.Draft
};

await repository.AddAsync(listing);
```

### البحث عن أفضل سعر
```csharp
var listings = await repository.SmartSearchAsync(new SmartSearchRequest
{
    Filters = new List<FilterItem>
    {
        new() { PropertyName = "ProductId", Operator = FilterOperator.Equals, Value = productId },
        new() { PropertyName = "Status", Operator = FilterOperator.Equals, Value = ListingStatus.Active },
        new() { PropertyName = "QuantityAvailable", Operator = FilterOperator.GreaterThan, Value = 0 }
    },
    OrderBy = "Price",
    Ascending = true
});

var bestPrice = listings.Items.FirstOrDefault();
```

### حجز كمية
```csharp
listing.QuantityAvailable -= quantity;
listing.QuantityReserved += quantity;

await repository.UpdateAsync(listing);
```

---

## ملاحظات تقنية

1. **Multi-Vendor**: كل بائع له عرضه الخاص للمنتج
2. **Inventory Tracking**: تتبع الكمية المتوفرة والمحجوزة
3. **Price Comparison**: دعم سعر المقارنة للخصومات
4. **Time-bound Offers**: دعم عروض محددة بوقت
5. **Analytics**: تتبع المبيعات والمشاهدات والتقييمات
