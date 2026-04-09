using ACommerce.SharedKernel.Abstractions.Entities;

namespace Ashare.Api2.Entities;

/// <summary>
/// عملية دفع. تُسجل كل دفعة عبر بوابة (Noon هنا).
/// </summary>
public class Payment : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid? BookingId { get; set; }
    public Guid CustomerId { get; set; }

    public string Gateway { get; set; } = "noon";
    public string? GatewayReference { get; set; }
    public string? PaymentUrl { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SAR";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public string? FailureReason { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public decimal? RefundedAmount { get; set; }

    /// <summary>معرف العملية المحاسبية في OperationEngine</summary>
    public Guid? OperationId { get; set; }
}

public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Captured = 2,
    Failed = 3,
    Refunded = 4,
    Voided = 5
}
