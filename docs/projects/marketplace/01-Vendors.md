# ACommerce.Vendors

## نظرة عامة
مكتبة إدارة البائعين (Vendors). تدعم Multi-Vendor Marketplace مع نظام عمولات مرن.

## الموقع
`/Marketplace/ACommerce.Vendors`

## التبعيات
- `ACommerce.SharedKernel.Abstractions`

---

## الكيانات (Entities)

### Vendor
البائع / التاجر:

```csharp
public class Vendor : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // الربط
    public Guid ProfileId { get; set; }

    // المتجر
    public required string StoreName { get; set; }
    public required string StoreSlug { get; set; }
    public string? Description { get; set; }
    public string? Logo { get; set; }
    public string? BannerImage { get; set; }

    // الحالة
    public VendorStatus Status { get; set; } = VendorStatus.Pending;

    // العمولات
    public CommissionType CommissionType { get; set; } = CommissionType.Percentage;
    public decimal CommissionValue { get; set; }
    public decimal? AdditionalFee { get; set; }
    public decimal MinimumPayout { get; set; } = 0;

    // الإحصائيات
    public decimal? Rating { get; set; }
    public int TotalSales { get; set; }

    // الرصيد
    public decimal AvailableBalance { get; set; }
    public decimal PendingBalance { get; set; }

    // البيانات التجارية
    public string? BankInfo { get; set; }       // مشفر
    public string? TaxInfo { get; set; }
    public string? CommercialRegister { get; set; }
    public string? TaxNumber { get; set; }

    // الموافقة
    public bool AgreedToTerms { get; set; }
    public DateTime? AgreedToTermsAt { get; set; }

    [NotMapped]
    public Dictionary<string, string> Metadata { get; set; } = new();
}
```

---

## التعدادات (Enums)

### VendorStatus

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `Pending` | 1 | قيد المراجعة |
| `Active` | 2 | نشط |
| `Suspended` | 3 | معلق |
| `Banned` | 4 | محظور |
| `Inactive` | 5 | غير نشط |

### CommissionType

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `Percentage` | 1 | نسبة مئوية (%) |
| `Fixed` | 2 | مبلغ ثابت |
| `Hybrid` | 3 | مختلط (نسبة + مبلغ) |

---

## حساب العمولات

### نسبة مئوية (Percentage)
```csharp
// CommissionValue = 10 (يعني 10%)
var commission = orderTotal * (vendor.CommissionValue / 100);
var vendorAmount = orderTotal - commission;
```

### مبلغ ثابت (Fixed)
```csharp
// CommissionValue = 5 (يعني 5 ريال لكل طلب)
var commission = vendor.CommissionValue;
var vendorAmount = orderTotal - commission;
```

### مختلط (Hybrid)
```csharp
// CommissionValue = 10% + AdditionalFee = 2 ريال
var percentageCommission = orderTotal * (vendor.CommissionValue / 100);
var totalCommission = percentageCommission + (vendor.AdditionalFee ?? 0);
var vendorAmount = orderTotal - totalCommission;
```

---

## DTOs

### CreateVendorDto
```csharp
public class CreateVendorDto
{
    public Guid ProfileId { get; set; }
    public required string StoreName { get; set; }
    public required string StoreSlug { get; set; }
    public string? Description { get; set; }
    public CommissionType CommissionType { get; set; }
    public decimal CommissionValue { get; set; }
}
```

### VendorResponseDto
```csharp
public class VendorResponseDto
{
    public Guid Id { get; set; }
    public string StoreName { get; set; }
    public string StoreSlug { get; set; }
    public string? Logo { get; set; }
    public VendorStatus Status { get; set; }
    public decimal? Rating { get; set; }
    public int TotalSales { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

## بنية الملفات
```
Marketplace/
├── ACommerce.Vendors/
│   ├── Entities/
│   │   └── Vendor.cs
│   ├── DTOs/
│   │   ├── CreateVendorDto.cs
│   │   └── VendorResponseDto.cs
│   └── Enums/
│       ├── VendorStatus.cs
│       └── CommissionType.cs
└── ACommerce.Vendors.Api/
    └── Controllers/
        └── VendorsController.cs
```

---

## مثال استخدام

### إنشاء بائع جديد
```csharp
var vendor = new Vendor
{
    ProfileId = profileId,
    StoreName = "متجر التقنية",
    StoreSlug = "tech-store",
    Description = "متجر متخصص في الإلكترونيات",
    CommissionType = CommissionType.Percentage,
    CommissionValue = 10,  // 10%
    Status = VendorStatus.Pending
};

await vendorRepository.AddAsync(vendor);
```

### تفعيل بائع
```csharp
vendor.Status = VendorStatus.Active;
await vendorRepository.UpdateAsync(vendor);
```

### حساب رصيد البائع
```csharp
// عند إكمال طلب
vendor.AvailableBalance += vendorAmount;
vendor.TotalSales++;
await vendorRepository.UpdateAsync(vendor);
```

---

## ملاحظات تقنية

1. **Multi-Vendor**: دعم بائعين متعددين
2. **Flexible Commission**: 3 أنواع عمولات
3. **Balance Tracking**: تتبع الرصيد المتاح والمعلق
4. **Store Branding**: لوجو وصورة غلاف
5. **Business Info**: سجل تجاري ورقم ضريبي
6. **Rating System**: نظام تقييم البائعين
