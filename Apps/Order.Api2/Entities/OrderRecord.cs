using ACommerce.SharedKernel.Abstractions.Entities;

namespace Order.Api2.Entities;

/// <summary>
/// طلب اوردر. لا توصيل ولا دفع إلكتروني — استلام من المتجر أو من السيارة فقط،
/// والدفع يتم في المتجر (نقدي أو بطاقة).
/// </summary>
public class OrderRecord : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string OrderNumber { get; set; } = default!;
    public Guid CustomerId { get; set; }
    public Guid VendorId { get; set; }

    // === pickup ===
    public PickupType PickupType { get; set; } = PickupType.InStore;

    // For Curbside pickup: car details (also stored on User as defaults).
    public string? CarModel { get; set; }
    public string? CarColor { get; set; }
    public string? CarPlate { get; set; }
    // Live customer location at the time of arrival (optional).
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }

    // === payment (offline only) ===
    public PaymentMethod PreferredPayment { get; set; } = PaymentMethod.Cash;
    /// <summary>المبلغ النقدي الذي سيقدّمه العميل (لحساب الباقي مسبقاً).</summary>
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

    // The accounting operation id that materialised this order
    public Guid? OperationId { get; set; }
}

public enum PickupType
{
    InStore = 0,
    Curbside = 1,
}

public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
}

public enum OrderStatus
{
    Pending   = 0,  // في انتظار قبول التاجر
    Accepted  = 1,  // مقبول، قيد التحضير
    Ready     = 2,  // جاهز للاستلام
    Delivered = 3,  // تم التسليم
    Cancelled = 4,  // ملغي
}
