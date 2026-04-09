using ACommerce.SharedKernel.Abstractions.Entities;

namespace Order.Api.Entities;

public class Notification : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    /// <summary>"order" | "promo" | "message" | "general"</summary>
    public string Type { get; set; } = "general";
    public string Priority { get; set; } = "normal";
    public string Channel { get; set; } = "inapp";
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? DeliveryStatus { get; set; }
    public DateTime? SentAt { get; set; }
}
