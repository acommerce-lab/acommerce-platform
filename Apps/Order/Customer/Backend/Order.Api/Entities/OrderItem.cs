using ACommerce.SharedKernel.Abstractions.Entities;

namespace Order.Api.Entities;

public class OrderItem : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid OrderId { get; set; }
    public Guid OfferId { get; set; }

    public string OfferTitle { get; set; } = default!;
    public string Emoji { get; set; } = "🍽️";
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
