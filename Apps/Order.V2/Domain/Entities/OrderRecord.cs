using ACommerce.SharedKernel.Abstractions.Entities;

namespace Order.V2.Domain;

public class OrderRecord : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string OrderNumber { get; set; } = default!;
    public Guid CustomerId { get; set; }
    public Guid VendorId { get; set; }

    public PickupType PickupType { get; set; } = PickupType.InStore;

    public string? CarModel { get; set; }
    public string? CarColor { get; set; }
    public string? CarPlate { get; set; }
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }

    public PaymentMethod PreferredPayment { get; set; } = PaymentMethod.Cash;
    public decimal? CashTendered { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "SAR";
    public decimal? ExpectedChange =>
        CashTendered.HasValue && CashTendered.Value >= Total
            ? CashTendered.Value - Total
            : (decimal?)null;

    public string? CustomerNotes { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public Guid? OperationId { get; set; }
}

public enum PickupType
{
    InStore  = 0,
    Curbside = 1,
}

public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
}

public enum OrderStatus
{
    Pending   = 0,
    Accepted  = 1,
    Ready     = 2,
    Delivered = 3,
    Cancelled = 4,
}
