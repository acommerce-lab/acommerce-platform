using ACommerce.SharedKernel.Abstractions.Entities;

namespace Order.Api.Entities;

public class Favorite : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }
    public Guid OfferId { get; set; }
}
