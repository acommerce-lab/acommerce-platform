using ACommerce.SharedKernel.Abstractions.Entities;

namespace Vendor.Api.Entities;

/// <summary>
/// نسخة المتجر من الطلب — تصله عبر webhook من Order.Api.
/// الحقول هنا للعرض والقرار فقط، مصدر الحقيقة يبقى في Order.Api.
/// </summary>
public class IncomingOrder : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>نفس الـ Id في Order.Api — يُستخدم في الـ callback.</summary>
    public Guid OrderApiId { get; set; }
    public Guid VendorId { get; set; }

    public string OrderNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string? CustomerPhone { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "SAR";
    public string? ItemsSummary { get; set; }
    public int PickupType { get; set; }       // 0=InStore, 1=Curbside
    public string? CarModel { get; set; }
    public string? CarColor { get; set; }
    public string? CarPlate { get; set; }
    public string? CustomerNotes { get; set; }

    public IncomingOrderStatus Status { get; set; } = IncomingOrderStatus.Pending;
    public DateTime ReceivedAt { get; set; }
    public DateTime TimeoutAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    /// <summary>Guid of the OpEngine operation that recorded the vendor's response.</summary>
    public Guid? ResponseOperationId { get; set; }
}

public enum IncomingOrderStatus
{
    Pending = 0,
    Accepted = 1,
    Ready = 2,
    Delivered = 3,
    Rejected = 4,
    TimedOut = 5
}
