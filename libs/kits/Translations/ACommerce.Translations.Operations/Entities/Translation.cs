using ACommerce.SharedKernel.Abstractions.Entities;

namespace ACommerce.Translations.Operations.Entities;

/// <summary>
/// ترجمة - يمكن ربطها بأي كيان وأي حقل.
/// </summary>
public class Translation : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>نوع الكيان: "Listing", "Category", "Product"</summary>
    public string EntityType { get; set; } = default!;

    /// <summary>معرف الكيان</summary>
    public Guid EntityId { get; set; }

    /// <summary>اسم الحقل: "Title", "Description"</summary>
    public string FieldName { get; set; } = default!;

    /// <summary>رمز اللغة: "ar", "en", "fr"</summary>
    public string Language { get; set; } = default!;

    /// <summary>النص المترجم</summary>
    public string TranslatedText { get; set; } = default!;

    /// <summary>هل المترجم موثق (محترف vs آلي)</summary>
    public bool IsVerified { get; set; }

    /// <summary>معرف المترجم</summary>
    public string? TranslatorId { get; set; }
}

/// <summary>
/// لغة مدعومة في النظام.
/// </summary>
public class Language : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>رمز اللغة (ar, en)</summary>
    public string Code { get; set; } = default!;

    /// <summary>الاسم بلغته الأصلية (العربية)</summary>
    public string NativeName { get; set; } = default!;

    /// <summary>الاسم بالإنجليزية</summary>
    public string EnglishName { get; set; } = default!;

    /// <summary>اتجاه الكتابة: "ltr", "rtl"</summary>
    public string Direction { get; set; } = "ltr";

    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}
