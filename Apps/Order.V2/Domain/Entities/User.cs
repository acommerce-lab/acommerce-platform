using ACommerce.SharedKernel.Abstractions.Entities;

namespace Order.V2.Domain;

public class User : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string PhoneNumber { get; set; } = default!;
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }

    public string Role { get; set; } = "customer";
    public bool IsActive { get; set; } = true;

    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "ar";

    public string? CarModel { get; set; }
    public string? CarColor { get; set; }
    public string? CarPlate { get; set; }
}
