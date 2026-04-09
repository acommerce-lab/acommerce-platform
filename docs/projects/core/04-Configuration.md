# ACommerce.Configuration

## نظرة عامة
مكتبة إدارة الإعدادات المرنة والقابلة للتخصيص. تدعم نطاقات متعددة (Global, Store, Vendor) مع إمكانية التشفير وأنواع بيانات متعددة.

## الموقع
`/Core/ACommerce.Configuration`

## التبعيات
- `ACommerce.SharedKernel.Abstractions`

---

## الكيانات (Entities)

### Setting
كيان الإعداد الأساسي:

```csharp
public class Setting : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // مفتاح الإعداد (Store.Name, Vendor.Commission, Payment.Moyasar.ApiKey)
    public required string Key { get; set; }

    // القيمة
    public required string Value { get; set; }

    // نطاق الإعداد (Global, Store, Vendor)
    public required string Scope { get; set; }

    // معرف النطاق (null للـ Global، StoreId أو VendorId)
    public Guid? ScopeId { get; set; }

    // نوع البيانات (String, Int, Bool, Json)
    public string DataType { get; set; } = "String";

    // الوصف
    public string? Description { get; set; }

    // مشفر؟
    public bool IsEncrypted { get; set; }

    // قابل للتعديل من قبل المستخدم؟
    public bool IsUserEditable { get; set; } = true;

    // بيانات إضافية [NotMapped]
    public Dictionary<string, string> Metadata { get; set; } = new();
}
```

### خصائص الإعداد

| الخاصية | النوع | الوصف | مثال |
|---------|------|-------|------|
| `Key` | `string` | مفتاح فريد للإعداد | `"Store.Name"`, `"Payment.Moyasar.ApiKey"` |
| `Value` | `string` | القيمة (مخزنة كنص) | `"متجري"`, `"sk_test_xxx"` |
| `Scope` | `string` | نطاق الإعداد | `"Global"`, `"Store"`, `"Vendor"` |
| `ScopeId` | `Guid?` | معرف النطاق | `null` للـ Global، أو معرف المتجر/البائع |
| `DataType` | `string` | نوع البيانات | `"String"`, `"Int"`, `"Bool"`, `"Json"` |
| `IsEncrypted` | `bool` | هل مشفر؟ | `true` للمفاتيح السرية |
| `IsUserEditable` | `bool` | قابل للتعديل؟ | `false` للإعدادات النظامية |

---

## الواجهات (Contracts)

### ISettingsProvider
واجهة مزود الإعدادات:

```csharp
public interface ISettingsProvider
{
    // الحصول على إعداد
    Task<T?> GetAsync<T>(
        string key,
        string scope = "Global",
        Guid? scopeId = null,
        CancellationToken cancellationToken = default);

    // حفظ إعداد
    Task SaveAsync<T>(
        string key,
        T value,
        string scope = "Global",
        Guid? scopeId = null,
        CancellationToken cancellationToken = default);

    // حذف إعداد
    Task DeleteAsync(
        string key,
        string scope = "Global",
        Guid? scopeId = null,
        CancellationToken cancellationToken = default);

    // الحصول على جميع إعدادات نطاق معين
    Task<Dictionary<string, string>> GetAllAsync(
        string scope = "Global",
        Guid? scopeId = null,
        CancellationToken cancellationToken = default);
}
```

### مثال الاستخدام
```csharp
// الحصول على إعداد عام
var storeName = await settingsProvider.GetAsync<string>("Store.Name");

// الحصول على إعداد بائع معين
var commission = await settingsProvider.GetAsync<decimal>(
    "Commission.Rate",
    scope: "Vendor",
    scopeId: vendorId);

// حفظ إعداد
await settingsProvider.SaveAsync(
    "Store.Name",
    "متجري الجديد",
    scope: "Global");

// الحصول على جميع إعدادات بائع
var vendorSettings = await settingsProvider.GetAllAsync(
    scope: "Vendor",
    scopeId: vendorId);
```

---

## إعدادات مُعرَّفة مسبقاً

### StoreSettings
إعدادات المتجر:

```csharp
public class StoreSettings
{
    public string StoreName { get; set; } = string.Empty;
    public string StoreUrl { get; set; } = string.Empty;
    public string Logo { get; set; } = string.Empty;
    public string DefaultCurrency { get; set; } = "SAR";
    public string DefaultLanguage { get; set; } = "ar";
    public bool AllowGuestCheckout { get; set; } = true;
    public bool AutoApproveVendors { get; set; }
    public bool AutoApproveProducts { get; set; }
    public decimal DefaultCommissionRate { get; set; } = 10;
    public int MinimumOrderAmount { get; set; }
    public bool EnableMultiVendor { get; set; } = true;
}
```

| الخاصية | الافتراضي | الوصف |
|---------|----------|-------|
| `StoreName` | `""` | اسم المتجر |
| `StoreUrl` | `""` | رابط المتجر |
| `Logo` | `""` | شعار المتجر |
| `DefaultCurrency` | `"SAR"` | العملة الافتراضية |
| `DefaultLanguage` | `"ar"` | اللغة الافتراضية |
| `AllowGuestCheckout` | `true` | السماح بالشراء بدون تسجيل |
| `AutoApproveVendors` | `false` | الموافقة التلقائية على البائعين |
| `AutoApproveProducts` | `false` | الموافقة التلقائية على المنتجات |
| `DefaultCommissionRate` | `10` | نسبة العمولة الافتراضية (%) |
| `MinimumOrderAmount` | `0` | الحد الأدنى للطلب |
| `EnableMultiVendor` | `true` | تفعيل نظام البائعين المتعددين |

### VendorSettings
إعدادات البائع:

```csharp
public class VendorSettings
{
    public Guid VendorId { get; set; }
    public bool EnableNotifications { get; set; } = true;
    public bool AutoConfirmOrders { get; set; }
    public int ProcessingTime { get; set; } = 3; // أيام
    public List<string> AllowedPaymentMethods { get; set; } = new();
    public List<string> AllowedShippingProviders { get; set; } = new();
    public Dictionary<string, string> CustomSettings { get; set; } = new();
}
```

| الخاصية | الافتراضي | الوصف |
|---------|----------|-------|
| `VendorId` | - | معرف البائع |
| `EnableNotifications` | `true` | تفعيل الإشعارات |
| `AutoConfirmOrders` | `false` | التأكيد التلقائي للطلبات |
| `ProcessingTime` | `3` | وقت معالجة الطلب (بالأيام) |
| `AllowedPaymentMethods` | `[]` | طرق الدفع المسموحة |
| `AllowedShippingProviders` | `[]` | مزودي الشحن المسموحين |
| `CustomSettings` | `{}` | إعدادات مخصصة |

---

## DTOs

### CreateSettingDto
```csharp
public class CreateSettingDto
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public string Scope { get; set; } = "Global";
    public Guid? ScopeId { get; set; }
    public string DataType { get; set; } = "String";
    public string? Description { get; set; }
    public bool IsEncrypted { get; set; }
}
```

### SettingResponseDto
```csharp
public class SettingResponseDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public Guid? ScopeId { get; set; }
    public string DataType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsUserEditable { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

## النطاقات (Scopes)

| النطاق | الوصف | مثال |
|--------|-------|------|
| `Global` | إعدادات على مستوى النظام | اسم المتجر، العملة الافتراضية |
| `Store` | إعدادات متجر معين | ساعات العمل، طرق الدفع |
| `Vendor` | إعدادات بائع معين | نسبة العمولة، إعدادات الشحن |

### الأولوية
عند جلب إعداد، يتم البحث بالترتيب:
1. **Vendor** (إذا كان `scopeId` بائع)
2. **Store** (إذا كان `scopeId` متجر)
3. **Global** (الافتراضي)

---

## أنواع البيانات (DataTypes)

| النوع | الوصف | مثال القيمة |
|-------|-------|------------|
| `String` | نص | `"متجري"` |
| `Int` | رقم صحيح | `"100"` |
| `Bool` | قيمة منطقية | `"true"`, `"false"` |
| `Json` | كائن JSON | `"{\"key\":\"value\"}"` |

---

## بنية الملفات
```
ACommerce.Configuration/
├── Contracts/
│   └── ISettingsProvider.cs    # واجهة + StoreSettings + VendorSettings
├── DTOs/
│   └── SettingDto.cs           # CreateSettingDto + SettingResponseDto
└── Entities/
    └── Setting.cs              # كيان الإعداد
```

---

## مثال استخدام كامل

### تكوين إعدادات المتجر
```csharp
// حفظ إعدادات المتجر
var storeSettings = new StoreSettings
{
    StoreName = "متجري",
    DefaultCurrency = "SAR",
    DefaultLanguage = "ar",
    AllowGuestCheckout = true,
    EnableMultiVendor = true,
    DefaultCommissionRate = 15
};

await settingsProvider.SaveAsync(
    "Store.Settings",
    storeSettings,
    scope: "Global");

// جلب إعدادات المتجر
var settings = await settingsProvider.GetAsync<StoreSettings>("Store.Settings");
```

### تكوين إعدادات بائع
```csharp
// حفظ إعدادات بائع
var vendorSettings = new VendorSettings
{
    VendorId = vendorId,
    EnableNotifications = true,
    ProcessingTime = 2,
    AllowedPaymentMethods = new() { "CreditCard", "ApplePay" },
    AllowedShippingProviders = new() { "SMSA", "Aramex" }
};

await settingsProvider.SaveAsync(
    "Vendor.Settings",
    vendorSettings,
    scope: "Vendor",
    scopeId: vendorId);
```

---

## ملاحظات تقنية

1. **Encryption**: الإعدادات المُشفرة تُخزَّن بشكل آمن
2. **Scoping**: نظام نطاقات مرن لفصل الإعدادات
3. **Type Safety**: دعم أنواع متعددة مع تحويل تلقائي
4. **Metadata**: دعم بيانات إضافية لكل إعداد (NotMapped)
5. **Soft Delete**: يدعم الحذف المنطقي عبر IBaseEntity
