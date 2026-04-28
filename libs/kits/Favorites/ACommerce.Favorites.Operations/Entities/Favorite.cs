using ACommerce.SharedKernel.Domain.Entities;

namespace ACommerce.Favorites.Operations.Entities;

/// <summary>
/// عنصر مفضل لمستخدم. عام (Generic) - يمكن أن يشير لأي كيان.
/// </summary>
public class Favorite : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }

    /// <summary>نوع الكيان المفضل: "Listing", "Vendor", "Product"</summary>
    public string EntityType { get; set; } = default!;

    /// <summary>معرف الكيان المفضل</summary>
    public Guid EntityId { get; set; }

    /// <summary>ملاحظة شخصية اختيارية</summary>
    public string? Note { get; set; }

    /// <summary>اسم القائمة (لدعم قوائم متعددة لكل مستخدم)</summary>
    public string ListName { get; set; } = "default";
}
