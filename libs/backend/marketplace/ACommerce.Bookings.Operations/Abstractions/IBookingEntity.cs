namespace ACommerce.Bookings.Operations.Abstractions;

/// <summary>Contract that any booking entity must satisfy to use BookingService.</summary>
public interface IBookingEntity
{
    Guid Id { get; }
    Guid OwnerId { get; }
    Guid CustomerId { get; }
    Guid ResourceId { get; }
    decimal TotalPrice { get; }
    string Currency { get; }
    int Status { get; set; }
    DateTime? UpdatedAt { get; set; }
    Guid? OperationId { get; set; }
}
