# ACommerce.Localization

## نظرة عامة
وحدة الترجمة والتعريب. توفر دعم متعدد اللغات لأي كيان في النظام.

## الموقع
`/Modules/ACommerce.Localization`

## التبعيات
- `ACommerce.SharedKernel.Abstractions`

---

## الكيانات (Entities)

### Translation
ترجمة حقل لكيان معين:

```csharp
public class Translation : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // نوع الكيان (Product, Category, Vendor, etc)
    public required string EntityType { get; set; }

    // معرف الكيان
    public required Guid EntityId { get; set; }

    // اسم الحقل (Name, Description, etc)
    public required string FieldName { get; set; }

    // اللغة (ar, en, fr, etc)
    public required string Language { get; set; }

    // النص المترجم
    public required string TranslatedText { get; set; }

    // مترجم موثق (من مترجم محترف أم آلي)
    public bool IsVerified { get; set; }

    // معرف المترجم
    public string? TranslatorId { get; set; }

    // بيانات إضافية
    [NotMapped] public Dictionary<string, string> Metadata { get; set; } = new();
}
```

### Language
تعريف اللغات المدعومة:

```csharp
public class Language : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // رمز اللغة (ar, en, fr)
    public required string Code { get; set; }

    // الاسم الأصلي (العربية, English, Français)
    public required string NativeName { get; set; }

    // الاسم بالإنجليزية
    public required string EnglishName { get; set; }

    // اتجاه الكتابة (ltr, rtl)
    public required string Direction { get; set; }

    // حالة اللغة
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }

    // ترتيب العرض
    public int SortOrder { get; set; }
}
```

---

## الواجهات (Contracts)

### ITranslatable
واجهة للكيانات القابلة للترجمة:

```csharp
public interface ITranslatable
{
    Guid Id { get; }
    string GetEntityType();
    IEnumerable<string> GetTranslatableFields();
}
```

### ITranslationService
خدمة الترجمة:

```csharp
public interface ITranslationService
{
    Task<string?> GetTranslationAsync(
        string entityType,
        Guid entityId,
        string fieldName,
        string language,
        CancellationToken cancellationToken = default);

    Task SaveTranslationAsync(
        string entityType,
        Guid entityId,
        string fieldName,
        string language,
        string translatedText,
        CancellationToken cancellationToken = default);

    Task DeleteTranslationAsync(
        string entityType,
        Guid entityId,
        string fieldName,
        string language,
        CancellationToken cancellationToken = default);
}
```

---

## DTOs

### CreateTranslationDto
```csharp
public class CreateTranslationDto
{
    public required string EntityType { get; set; }
    public required Guid EntityId { get; set; }
    public required string FieldName { get; set; }
    public required string Language { get; set; }
    public required string TranslatedText { get; set; }
}
```

### TranslationResponseDto
```csharp
public class TranslationResponseDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string FieldName { get; set; }
    public string Language { get; set; }
    public string TranslatedText { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### LanguageDto
```csharp
public class LanguageDto
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string NativeName { get; set; }
    public string EnglishName { get; set; }
    public string Direction { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
}
```

---

## بنية الملفات
```
ACommerce.Localization/
├── Contracts/
│   └── ITranslatable.cs      # ITranslatable + ITranslationService
├── Entities/
│   └── Translation.cs        # Translation + Language
└── DTOs/
    └── TranslationDto.cs     # جميع DTOs
```

---

## مثال استخدام

### ترجمة اسم منتج
```csharp
await translationService.SaveTranslationAsync(
    entityType: "Product",
    entityId: productId,
    fieldName: "Name",
    language: "en",
    translatedText: "Laptop Computer"
);
```

### الحصول على ترجمة
```csharp
var translatedName = await translationService.GetTranslationAsync(
    entityType: "Product",
    entityId: productId,
    fieldName: "Name",
    language: "en"
);
```

### تنفيذ ITranslatable في كيان
```csharp
public class Product : IBaseEntity, ITranslatable
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    public string GetEntityType() => "Product";

    public IEnumerable<string> GetTranslatableFields()
    {
        yield return "Name";
        yield return "Description";
    }
}
```

### إعداد اللغات المدعومة
```csharp
var languages = new List<Language>
{
    new Language
    {
        Code = "ar",
        NativeName = "العربية",
        EnglishName = "Arabic",
        Direction = "rtl",
        IsActive = true,
        IsDefault = true,
        SortOrder = 1
    },
    new Language
    {
        Code = "en",
        NativeName = "English",
        EnglishName = "English",
        Direction = "ltr",
        IsActive = true,
        SortOrder = 2
    }
};
```

---

## ملاحظات تقنية

1. **Entity Agnostic**: قابل للتطبيق على أي كيان عبر EntityType و EntityId
2. **Field Level**: ترجمة على مستوى الحقل وليس الكيان
3. **RTL Support**: دعم كامل للغات ذات الاتجاه من اليمين لليسار
4. **Verification**: تمييز الترجمات الموثقة من الآلية
5. **Multiple Languages**: دعم عدد غير محدود من اللغات
6. **Default Language**: تحديد لغة افتراضية للنظام
