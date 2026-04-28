using ACommerce.SharedKernel.Domain.Entities;

namespace ACommerce.OrderPlatform.Entities;

/// <summary>
/// مستخدم تاجر — تسجيل الدخول برقم الجوال (SMS OTP).
/// مستقل تماماً عن Order.Api — كل خدمة تُدير مستخدميها بنفسها.
/// </summary>
public class VendorUser : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string PhoneNumber { get; set; } = default!;
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>"vendor" أو "admin"</summary>
    public string Role { get; set; } = "vendor";
    public bool IsActive { get; set; } = true;

    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "ar";
}
