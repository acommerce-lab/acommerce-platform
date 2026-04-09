# ACommerce.Catalog.Attributes

## نظرة عامة | Overview

مكتبة `ACommerce.Catalog.Attributes` توفر نظام خصائص ديناميكية (Dynamic Attributes) يسمح بإضافة خصائص مخصصة للمنتجات دون الحاجة لتعديل قاعدة البيانات. يدعم النظام أنواع بيانات متعددة وقواعد تحقق مرنة.

This library provides a dynamic attributes system that allows adding custom properties to products without modifying the database schema. The system supports multiple data types and flexible validation rules.

**المسار | Path:** `Catalog/ACommerce.Catalog.Attributes`
**نوع المشروع | Project Type:** Class Library (.NET 9.0)
**الاعتماديات | Dependencies:**
- ACommerce.SharedKernel.Abstractions
- ACommerce.SharedKernel.CQRS

---

## لماذا الخصائص الديناميكية؟ | Why Dynamic Attributes?

### المشكلة التقليدية | Traditional Problem

```csharp
// ❌ إضافة حقول جديدة تتطلب تغيير الـ Schema
public class Product
{
    // حقول ثابتة للإلكترونيات
    public string? ScreenSize { get; set; }
    public int? BatteryCapacity { get; set; }
    public string? Processor { get; set; }

    // حقول ثابتة للملابس
    public string? Material { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }

    // حقول ثابتة للأطعمة
    public DateTime? ExpiryDate { get; set; }
    public string? Ingredients { get; set; }

    // ... مئات الحقول الأخرى
}
```

### الحل الديناميكي | Dynamic Solution

```csharp
// ✅ خصائص ديناميكية - مرنة وقابلة للتوسع
public class Product
{
    public ICollection<ProductAttribute> Attributes { get; set; }
}

// تعريف الخصائص حسب التصنيف
var electronicsAttributes = new[]
{
    new AttributeDefinition { Name = "حجم الشاشة", Type = AttributeType.Text },
    new AttributeDefinition { Name = "سعة البطارية", Type = AttributeType.Number },
    new AttributeDefinition { Name = "المعالج", Type = AttributeType.SingleSelect },
};
```

---

## نموذج البيانات | Data Model

### AttributeDefinition

تعريف الخاصية - يحدد اسم الخاصية ونوعها وقواعد التحقق.

```csharp
public class AttributeDefinition : IEntity<Guid>, IAuditableEntity, IMultiTenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>
    /// اسم الخاصية (مثال: اللون، المقاس)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// الاسم الفريد للنظام (color, size)
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// وصف الخاصية
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// نوع البيانات
    /// </summary>
    public AttributeType Type { get; set; }

    /// <summary>
    /// نوع العرض في واجهة المستخدم
    /// </summary>
    public AttributeDisplayType DisplayType { get; set; }

    /// <summary>
    /// هل الخاصية مطلوبة؟
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// هل تستخدم للفلترة في البحث؟
    /// </summary>
    public bool IsFilterable { get; set; }

    /// <summary>
    /// هل تظهر في صفحة المنتج؟
    /// </summary>
    public bool IsVisibleOnProductPage { get; set; } = true;

    /// <summary>
    /// هل تستخدم لإنشاء المتغيرات؟
    /// </summary>
    public bool UsedForVariants { get; set; }

    /// <summary>
    /// ترتيب العرض
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// القيمة الافتراضية
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// التصنيفات المرتبطة (إن كانت محددة)
    /// </summary>
    public ICollection<AttributeCategoryMapping> CategoryMappings { get; set; } = new List<AttributeCategoryMapping>();

    /// <summary>
    /// القيم المحددة مسبقاً (للقوائم المنسدلة)
    /// </summary>
    public ICollection<AttributeOption> Options { get; set; } = new List<AttributeOption>();

    /// <summary>
    /// قواعد التحقق
    /// </summary>
    public AttributeValidation? Validation { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
```

### AttributeType

أنواع البيانات المدعومة.

```csharp
public enum AttributeType
{
    /// <summary>
    /// نص قصير
    /// </summary>
    Text = 0,

    /// <summary>
    /// نص طويل (متعدد الأسطر)
    /// </summary>
    TextArea = 1,

    /// <summary>
    /// رقم صحيح
    /// </summary>
    Integer = 2,

    /// <summary>
    /// رقم عشري
    /// </summary>
    Decimal = 3,

    /// <summary>
    /// نعم/لا
    /// </summary>
    Boolean = 4,

    /// <summary>
    /// تاريخ
    /// </summary>
    Date = 5,

    /// <summary>
    /// تاريخ ووقت
    /// </summary>
    DateTime = 6,

    /// <summary>
    /// اختيار واحد من قائمة
    /// </summary>
    SingleSelect = 7,

    /// <summary>
    /// اختيارات متعددة من قائمة
    /// </summary>
    MultiSelect = 8,

    /// <summary>
    /// لون (HEX)
    /// </summary>
    Color = 9,

    /// <summary>
    /// رابط URL
    /// </summary>
    Url = 10,

    /// <summary>
    /// بريد إلكتروني
    /// </summary>
    Email = 11,

    /// <summary>
    /// ملف/صورة
    /// </summary>
    File = 12,

    /// <summary>
    /// JSON مخصص
    /// </summary>
    Json = 13
}
```

### AttributeDisplayType

طريقة عرض الخاصية في واجهة المستخدم.

```csharp
public enum AttributeDisplayType
{
    /// <summary>
    /// حقل نص عادي
    /// </summary>
    TextField = 0,

    /// <summary>
    /// منطقة نص
    /// </summary>
    TextArea = 1,

    /// <summary>
    /// قائمة منسدلة
    /// </summary>
    Dropdown = 2,

    /// <summary>
    /// أزرار راديو
    /// </summary>
    RadioButtons = 3,

    /// <summary>
    /// مربعات اختيار
    /// </summary>
    Checkboxes = 4,

    /// <summary>
    /// أزرار مجموعة
    /// </summary>
    ButtonGroup = 5,

    /// <summary>
    /// عينات ألوان
    /// </summary>
    ColorSwatch = 6,

    /// <summary>
    /// عينات صور
    /// </summary>
    ImageSwatch = 7,

    /// <summary>
    /// شريط تمرير (للأرقام)
    /// </summary>
    Slider = 8,

    /// <summary>
    /// نطاق (من - إلى)
    /// </summary>
    Range = 9,

    /// <summary>
    /// منتقي تاريخ
    /// </summary>
    DatePicker = 10,

    /// <summary>
    /// مفتاح تبديل
    /// </summary>
    Toggle = 11
}
```

### AttributeOption

القيم المحددة مسبقاً للخصائص من نوع Select.

```csharp
public class AttributeOption : IEntity<Guid>
{
    public Guid Id { get; set; }
    public Guid AttributeDefinitionId { get; set; }
    public AttributeDefinition Definition { get; set; } = null!;

    /// <summary>
    /// القيمة المخزنة
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// النص المعروض
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// قيمة إضافية (مثل كود اللون)
    /// </summary>
    public string? ExtraValue { get; set; }

    /// <summary>
    /// رابط الصورة (للعينات)
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// هل القيمة نشطة؟
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// ترتيب العرض
    /// </summary>
    public int DisplayOrder { get; set; }
}
```

### AttributeValidation

قواعد التحقق من صحة القيم.

```csharp
public class AttributeValidation
{
    /// <summary>
    /// الحد الأدنى للقيمة (للأرقام)
    /// </summary>
    public decimal? MinValue { get; set; }

    /// <summary>
    /// الحد الأقصى للقيمة (للأرقام)
    /// </summary>
    public decimal? MaxValue { get; set; }

    /// <summary>
    /// الحد الأدنى لعدد الأحرف (للنصوص)
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// الحد الأقصى لعدد الأحرف (للنصوص)
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// نمط التحقق (Regex)
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// رسالة خطأ مخصصة
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// الحد الأدنى للاختيارات (للـ MultiSelect)
    /// </summary>
    public int? MinSelections { get; set; }

    /// <summary>
    /// الحد الأقصى للاختيارات (للـ MultiSelect)
    /// </summary>
    public int? MaxSelections { get; set; }

    /// <summary>
    /// أقصى حجم للملف (بالميجابايت)
    /// </summary>
    public int? MaxFileSizeMb { get; set; }

    /// <summary>
    /// الامتدادات المسموحة للملفات
    /// </summary>
    public List<string>? AllowedFileExtensions { get; set; }
}
```

### ProductAttribute

قيمة الخاصية المرتبطة بالمنتج.

```csharp
public class ProductAttribute : IEntity<Guid>
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public Guid AttributeDefinitionId { get; set; }
    public AttributeDefinition Definition { get; set; } = null!;

    // القيم المخزنة (حسب النوع)
    public string? TextValue { get; set; }
    public decimal? NumberValue { get; set; }
    public bool? BooleanValue { get; set; }
    public DateTime? DateValue { get; set; }
    public List<string>? MultiSelectValues { get; set; }
    public string? JsonValue { get; set; }

    /// <summary>
    /// الحصول على القيمة المناسبة حسب نوع الخاصية
    /// </summary>
    public object? GetValue()
    {
        return Definition.Type switch
        {
            AttributeType.Text or AttributeType.TextArea or
            AttributeType.Url or AttributeType.Email or
            AttributeType.Color or AttributeType.SingleSelect => TextValue,

            AttributeType.Integer => NumberValue.HasValue ? (int)NumberValue.Value : null,
            AttributeType.Decimal => NumberValue,

            AttributeType.Boolean => BooleanValue,

            AttributeType.Date or AttributeType.DateTime => DateValue,

            AttributeType.MultiSelect => MultiSelectValues,

            AttributeType.Json => JsonValue,

            _ => TextValue
        };
    }

    /// <summary>
    /// تعيين القيمة مع التحقق من النوع
    /// </summary>
    public void SetValue(object? value)
    {
        // Reset all values
        TextValue = null;
        NumberValue = null;
        BooleanValue = null;
        DateValue = null;
        MultiSelectValues = null;
        JsonValue = null;

        if (value == null) return;

        switch (Definition.Type)
        {
            case AttributeType.Text:
            case AttributeType.TextArea:
            case AttributeType.Url:
            case AttributeType.Email:
            case AttributeType.Color:
            case AttributeType.SingleSelect:
                TextValue = value.ToString();
                break;

            case AttributeType.Integer:
            case AttributeType.Decimal:
                NumberValue = Convert.ToDecimal(value);
                break;

            case AttributeType.Boolean:
                BooleanValue = Convert.ToBoolean(value);
                break;

            case AttributeType.Date:
            case AttributeType.DateTime:
                DateValue = Convert.ToDateTime(value);
                break;

            case AttributeType.MultiSelect:
                MultiSelectValues = value as List<string> ?? new List<string> { value.ToString()! };
                break;

            case AttributeType.Json:
                JsonValue = value is string str ? str : JsonSerializer.Serialize(value);
                break;
        }
    }
}
```

---

## خدمة التحقق | Validation Service

```csharp
public interface IAttributeValidationService
{
    ValidationResult Validate(AttributeDefinition definition, object? value);
    Task<ValidationResult> ValidateProductAttributesAsync(
        Guid productId,
        Dictionary<Guid, object> attributeValues,
        CancellationToken cancellationToken = default);
}

public class AttributeValidationService : IAttributeValidationService
{
    public ValidationResult Validate(AttributeDefinition definition, object? value)
    {
        var errors = new List<string>();

        // Required check
        if (definition.IsRequired && IsEmpty(value))
        {
            errors.Add($"الخاصية '{definition.Name}' مطلوبة");
            return ValidationResult.Failure(errors);
        }

        if (IsEmpty(value)) return ValidationResult.Success();

        var validation = definition.Validation;
        if (validation == null) return ValidationResult.Success();

        // Type-specific validation
        switch (definition.Type)
        {
            case AttributeType.Text:
            case AttributeType.TextArea:
                ValidateText(value?.ToString(), validation, definition.Name, errors);
                break;

            case AttributeType.Integer:
            case AttributeType.Decimal:
                ValidateNumber(value, validation, definition.Name, errors);
                break;

            case AttributeType.MultiSelect:
                ValidateMultiSelect(value as List<string>, validation, definition.Name, errors);
                break;

            case AttributeType.Email:
                ValidateEmail(value?.ToString(), definition.Name, errors);
                break;

            case AttributeType.Url:
                ValidateUrl(value?.ToString(), definition.Name, errors);
                break;
        }

        return errors.Any()
            ? ValidationResult.Failure(errors)
            : ValidationResult.Success();
    }

    private static void ValidateText(string? value, AttributeValidation validation, string name, List<string> errors)
    {
        if (value == null) return;

        if (validation.MinLength.HasValue && value.Length < validation.MinLength)
            errors.Add($"'{name}' يجب أن يكون {validation.MinLength} أحرف على الأقل");

        if (validation.MaxLength.HasValue && value.Length > validation.MaxLength)
            errors.Add($"'{name}' يجب ألا يتجاوز {validation.MaxLength} حرف");

        if (!string.IsNullOrEmpty(validation.Pattern) &&
            !Regex.IsMatch(value, validation.Pattern))
            errors.Add(validation.ErrorMessage ?? $"'{name}' بتنسيق غير صحيح");
    }

    private static void ValidateNumber(object? value, AttributeValidation validation, string name, List<string> errors)
    {
        if (value == null) return;

        var number = Convert.ToDecimal(value);

        if (validation.MinValue.HasValue && number < validation.MinValue)
            errors.Add($"'{name}' يجب أن يكون {validation.MinValue} على الأقل");

        if (validation.MaxValue.HasValue && number > validation.MaxValue)
            errors.Add($"'{name}' يجب ألا يتجاوز {validation.MaxValue}");
    }

    private static void ValidateMultiSelect(List<string>? values, AttributeValidation validation, string name, List<string> errors)
    {
        if (values == null) return;

        if (validation.MinSelections.HasValue && values.Count < validation.MinSelections)
            errors.Add($"'{name}' يتطلب اختيار {validation.MinSelections} على الأقل");

        if (validation.MaxSelections.HasValue && values.Count > validation.MaxSelections)
            errors.Add($"'{name}' لا يمكن اختيار أكثر من {validation.MaxSelections}");
    }

    private static void ValidateEmail(string? value, string name, List<string> errors)
    {
        if (string.IsNullOrEmpty(value)) return;

        if (!Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            errors.Add($"'{name}' بريد إلكتروني غير صحيح");
    }

    private static void ValidateUrl(string? value, string name, List<string> errors)
    {
        if (string.IsNullOrEmpty(value)) return;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            errors.Add($"'{name}' رابط غير صحيح");
    }

    private static bool IsEmpty(object? value)
    {
        return value switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            IList list => list.Count == 0,
            _ => false
        };
    }
}
```

---

## أمثلة استخدام | Usage Examples

### تعريف خصائص لتصنيف الإلكترونيات

```csharp
var electronicsAttributes = new List<AttributeDefinition>
{
    new()
    {
        Name = "حجم الشاشة",
        Code = "screen_size",
        Type = AttributeType.Decimal,
        DisplayType = AttributeDisplayType.TextField,
        IsRequired = true,
        IsFilterable = true,
        Validation = new AttributeValidation
        {
            MinValue = 1,
            MaxValue = 100,
            ErrorMessage = "حجم الشاشة يجب أن يكون بين 1 و 100 بوصة"
        }
    },
    new()
    {
        Name = "اللون",
        Code = "color",
        Type = AttributeType.SingleSelect,
        DisplayType = AttributeDisplayType.ColorSwatch,
        IsRequired = true,
        IsFilterable = true,
        UsedForVariants = true,
        Options = new List<AttributeOption>
        {
            new() { Value = "black", Label = "أسود", ExtraValue = "#000000" },
            new() { Value = "white", Label = "أبيض", ExtraValue = "#FFFFFF" },
            new() { Value = "silver", Label = "فضي", ExtraValue = "#C0C0C0" },
            new() { Value = "gold", Label = "ذهبي", ExtraValue = "#FFD700" },
        }
    },
    new()
    {
        Name = "سعة التخزين",
        Code = "storage",
        Type = AttributeType.SingleSelect,
        DisplayType = AttributeDisplayType.ButtonGroup,
        IsRequired = true,
        IsFilterable = true,
        UsedForVariants = true,
        Options = new List<AttributeOption>
        {
            new() { Value = "64", Label = "64GB" },
            new() { Value = "128", Label = "128GB" },
            new() { Value = "256", Label = "256GB" },
            new() { Value = "512", Label = "512GB" },
            new() { Value = "1024", Label = "1TB" },
        }
    },
    new()
    {
        Name = "المواصفات",
        Code = "specifications",
        Type = AttributeType.Json,
        DisplayType = AttributeDisplayType.TextArea,
        IsRequired = false,
        IsVisibleOnProductPage = true
    }
};
```

### تعريف خصائص لتصنيف الملابس

```csharp
var clothingAttributes = new List<AttributeDefinition>
{
    new()
    {
        Name = "المقاس",
        Code = "size",
        Type = AttributeType.SingleSelect,
        DisplayType = AttributeDisplayType.ButtonGroup,
        IsRequired = true,
        IsFilterable = true,
        UsedForVariants = true,
        Options = new List<AttributeOption>
        {
            new() { Value = "xs", Label = "XS" },
            new() { Value = "s", Label = "S" },
            new() { Value = "m", Label = "M" },
            new() { Value = "l", Label = "L" },
            new() { Value = "xl", Label = "XL" },
            new() { Value = "xxl", Label = "XXL" },
        }
    },
    new()
    {
        Name = "الخامة",
        Code = "material",
        Type = AttributeType.MultiSelect,
        DisplayType = AttributeDisplayType.Checkboxes,
        IsRequired = true,
        IsFilterable = true,
        Validation = new AttributeValidation
        {
            MinSelections = 1,
            MaxSelections = 5
        },
        Options = new List<AttributeOption>
        {
            new() { Value = "cotton", Label = "قطن" },
            new() { Value = "polyester", Label = "بوليستر" },
            new() { Value = "silk", Label = "حرير" },
            new() { Value = "wool", Label = "صوف" },
            new() { Value = "linen", Label = "كتان" },
        }
    },
    new()
    {
        Name = "تعليمات الغسيل",
        Code = "care_instructions",
        Type = AttributeType.TextArea,
        DisplayType = AttributeDisplayType.TextArea,
        IsRequired = false,
        Validation = new AttributeValidation
        {
            MaxLength = 500
        }
    }
};
```

### تعيين قيم الخصائص للمنتج

```csharp
var product = new Product
{
    Name = "iPhone 15 Pro",
    Sku = "IPHONE-15-PRO",
    BasePrice = 4999
};

// تعيين الخصائص
var attributes = new List<ProductAttribute>
{
    new()
    {
        AttributeDefinitionId = screenSizeAttr.Id,
        NumberValue = 6.1m
    },
    new()
    {
        AttributeDefinitionId = colorAttr.Id,
        TextValue = "black"
    },
    new()
    {
        AttributeDefinitionId = storageAttr.Id,
        TextValue = "256"
    },
    new()
    {
        AttributeDefinitionId = specificationsAttr.Id,
        JsonValue = JsonSerializer.Serialize(new
        {
            processor = "A17 Pro",
            ram = "8GB",
            camera = "48MP + 12MP + 12MP",
            battery = "3274mAh"
        })
    }
};

product.Attributes = attributes;
```

---

## الفلترة بالخصائص | Filtering by Attributes

```csharp
public async Task<SmartSearchResult<ProductListDto>> SearchWithFiltersAsync(
    Dictionary<Guid, List<string>> attributeFilters,
    CancellationToken cancellationToken)
{
    var query = _context.Products
        .Include(p => p.Attributes)
        .AsQueryable();

    foreach (var filter in attributeFilters)
    {
        var attributeId = filter.Key;
        var values = filter.Value;

        query = query.Where(p =>
            p.Attributes.Any(a =>
                a.AttributeDefinitionId == attributeId &&
                (a.TextValue != null && values.Contains(a.TextValue)) ||
                (a.MultiSelectValues != null && a.MultiSelectValues.Any(v => values.Contains(v)))
            )
        );
    }

    // ...
}
```

---

## تسجيل الخدمات | Service Registration

```csharp
public static class AttributesServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogAttributes(
        this IServiceCollection services)
    {
        services.AddScoped<IRepository<AttributeDefinition, Guid>,
            EfCoreRepository<AttributeDefinition, Guid>>();
        services.AddScoped<IRepository<AttributeOption, Guid>,
            EfCoreRepository<AttributeOption, Guid>>();
        services.AddScoped<IAttributeValidationService, AttributeValidationService>();

        return services;
    }
}
```

---

## المراجع | References

- [EAV Model](https://en.wikipedia.org/wiki/Entity%E2%80%93attribute%E2%80%93value_model)
- [Dynamic Attributes in E-commerce](https://www.shopify.com/blog/product-attributes)
