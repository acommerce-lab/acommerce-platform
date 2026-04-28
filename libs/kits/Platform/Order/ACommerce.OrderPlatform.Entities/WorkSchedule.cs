using ACommerce.SharedKernel.Domain.Entities;

namespace ACommerce.OrderPlatform.Entities;

/// <summary>
/// يوم واحد في جدول عمل المتجر. لكل يوم سطر واحد.
/// IsOff = true يعني المتجر مغلق في هذا اليوم بالكامل.
/// </summary>
public class WorkSchedule : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid VendorId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>وقت الفتح بصيغة "HH:mm" (24-hour).</summary>
    public string OpenTime { get; set; } = "07:00";
    /// <summary>وقت الإغلاق بصيغة "HH:mm".</summary>
    public string CloseTime { get; set; } = "23:00";

    public bool IsOff { get; set; }
}
