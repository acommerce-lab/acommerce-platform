using ACommerce.SharedKernel.Abstractions.Entities;

namespace Order.V2.Api.Entities;

public class Vendor : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid OwnerId { get; set; }
    public Guid CategoryId { get; set; }

    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string City { get; set; } = "الرياض";
    public string? District { get; set; }
    public string Phone { get; set; } = default!;
    public string LogoEmoji { get; set; } = "🍽️";
    public string CoverEmoji { get; set; } = "🍔";

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string OpenHours { get; set; } = "07:00|23:00";

    public bool IsActive { get; set; } = true;
    public double Rating { get; set; } = 4.5;
    public int RatingCount { get; set; } = 0;
}
