using ACommerce.SharedKernel.Abstractions.Entities;

namespace Order.Api2.Entities;

public class Category : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string Slug { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string Icon { get; set; } = "🍔";
    public int SortOrder { get; set; }
}
