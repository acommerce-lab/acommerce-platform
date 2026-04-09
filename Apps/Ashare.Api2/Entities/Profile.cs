using ACommerce.SharedKernel.Abstractions.Entities;

namespace Ashare.Api2.Entities;

/// <summary>
/// ملف شخصي مفصّل لمستخدم.
/// </summary>
public class Profile : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? CoverUrl { get; set; }
    public string? Bio { get; set; }
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string PreferredLanguage { get; set; } = "ar";

    /// <summary>تفضيلات الاتصال</summary>
    public bool IsPhonePublic { get; set; } = true;
    public bool IsEmailPublic { get; set; }

    /// <summary>عدد العروض المنشورة</summary>
    public int ListingsCount { get; set; }

    /// <summary>متوسط التقييم (0-5)</summary>
    public double? Rating { get; set; }
    public int RatingCount { get; set; }
}

/// <summary>
/// سجل ملف مرفوع - نتتبع كل ملف نُخزّنه عبر المخزن.
/// </summary>
public class MediaFile : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UploaderId { get; set; }
    public string FileName { get; set; } = default!;
    public string FilePath { get; set; } = default!;
    public string PublicUrl { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string Directory { get; set; } = "default";
    public string Provider { get; set; } = default!; // "AliyunOSS", "Local", ...

    /// <summary>الكيان المرتبط (اختياري)</summary>
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }

    /// <summary>معرف العملية المحاسبية</summary>
    public Guid? OperationId { get; set; }
}
