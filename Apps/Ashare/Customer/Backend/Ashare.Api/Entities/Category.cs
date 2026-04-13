using ACommerce.SharedKernel.Abstractions.Entities;

namespace Ashare.Api.Entities;

/// <summary>
/// فئة المساحة في عشير. تطابق الفئات الخمس المبذورة في خدمة Ashare الحالية:
/// Residential, LookingForHousing, LookingForPartner, Administrative, Commercial.
/// </summary>
public class Category : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string Slug { get; set; } = default!;       // "residential", "commercial", ...
    public string NameAr { get; set; } = default!;     // "سكني"
    public string NameEn { get; set; } = default!;     // "Residential"
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
