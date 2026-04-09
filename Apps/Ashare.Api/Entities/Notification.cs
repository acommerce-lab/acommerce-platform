using ACommerce.SharedKernel.Abstractions.Entities;

namespace Ashare.Api.Entities;

/// <summary>
/// إشعار. يطابق بنية الإشعارات في عشير الحالية.
/// </summary>
public class Notification : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string Type { get; set; } = "info";    // info, booking, payment, message, system
    public string Priority { get; set; } = "normal"; // low, normal, high, urgent
    public string Channel { get; set; } = "inapp";   // inapp, push, email, sms

    public string? ActionUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? DataJson { get; set; }       // JSON بيانات إضافية

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? SentAt { get; set; }

    public string DeliveryStatus { get; set; } = "pending"; // pending, sent, failed
    public string? FailureReason { get; set; }
}
