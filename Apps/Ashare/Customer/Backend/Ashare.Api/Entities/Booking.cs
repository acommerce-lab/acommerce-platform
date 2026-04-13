using ACommerce.SharedKernel.Abstractions.Entities;

namespace Ashare.Api.Entities;

/// <summary>
/// حجز عرض. عشير حالياً يستخدم Bookings من ACommerce.Bookings.
/// هنا كيان مبسط مخصص لعمليات الحجز.
/// </summary>
public class Booking : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid ListingId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid OwnerId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "SAR";

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public string? Notes { get; set; }
    public Guid? PaymentId { get; set; }

    /// <summary>معرف العملية المحاسبية في OperationEngine</summary>
    public Guid? OperationId { get; set; }
}

public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    AwaitingPayment = 2,
    Paid = 3,
    Cancelled = 4,
    Completed = 5,
    Refunded = 6
}
