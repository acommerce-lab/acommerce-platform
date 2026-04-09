using ACommerce.SharedKernel.Abstractions.Entities;

namespace Ashare.Api2.Entities;

/// <summary>
/// مستخدم عشير. يطابق بنية مستخدم Ashare الحالية.
/// </summary>
public class User : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string PhoneNumber { get; set; } = default!;
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? NationalId { get; set; }

    /// <summary>هل تم التحقق من الهوية عبر نفاذ</summary>
    public bool NafathVerified { get; set; }

    /// <summary>هل المستخدم مفعّل (تخطى المصادقة الثنائية)</summary>
    public bool IsActive { get; set; }

    /// <summary>دور المستخدم: customer, owner, admin</summary>
    public string Role { get; set; } = "customer";
}
