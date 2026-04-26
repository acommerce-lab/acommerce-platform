using ACommerce.SharedKernel.Abstractions.Entities;

namespace ACommerce.OrderPlatform.Entities;

/// <summary>
/// إعدادات قبول الطلبات لمتجر واحد.
/// AcceptingOrders = المفتاح الرئيسي (التاجر يقدر يغلقه يدوياً).
/// OrderTimeoutMinutes = المهلة قبل الإلغاء التلقائي للطلب المعلّق.
/// </summary>
public class VendorSettings : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid VendorId { get; set; }

    /// <summary>هل المتجر يقبل طلبات حالياً؟ التاجر يتحكم فيه يدوياً.</summary>
    public bool AcceptingOrders { get; set; } = true;

    /// <summary>الحد الأقصى للطلبات المعلقة المسموحة في نفس الوقت. 0 = بلا حد.</summary>
    public int MaxConcurrentPending { get; set; }

    /// <summary>عدد الدقائق قبل الإلغاء التلقائي للطلب المعلّق. الافتراضي 10 دقائق.</summary>
    public int OrderTimeoutMinutes { get; set; } = 10;
}
