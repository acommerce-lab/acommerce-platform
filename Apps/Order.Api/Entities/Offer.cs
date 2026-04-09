using ACommerce.SharedKernel.Abstractions.Entities;

namespace Order.Api.Entities;

/// <summary>
/// عرض اوردر — صنف من قائمة التاجر بسعر خاص. يدعم سعر مقارن لعرض الخصم.
/// </summary>
public class Offer : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid VendorId { get; set; }
    public Guid CategoryId { get; set; }

    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string Currency { get; set; } = "SAR";
    public string Emoji { get; set; } = "🍽️";

    public int QuantityAvailable { get; set; } = 100;
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }

    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    public int DiscountPercent =>
        OriginalPrice.HasValue && OriginalPrice.Value > 0
            ? (int)Math.Round((1 - Price / OriginalPrice.Value) * 100)
            : 0;
}
