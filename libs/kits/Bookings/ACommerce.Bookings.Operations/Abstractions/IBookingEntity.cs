namespace ACommerce.Bookings.Operations.Abstractions;

/// <summary>
/// Contract that any booking entity must satisfy to use BookingService.
/// Note: Id and UpdatedAt are inherited via the IBaseEntity constraint on BookingService&lt;T&gt;,
/// so they are intentionally not redeclared here (would cause member ambiguity).
/// </summary>
public interface IBookingEntity
{
    Guid OwnerId { get; }
    Guid CustomerId { get; }
    Guid ResourceId { get; }
    decimal TotalPrice { get; }
    string Currency { get; }
    int Status { get; set; }
    Guid? OperationId { get; set; }
}
