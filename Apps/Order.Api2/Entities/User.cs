using ACommerce.SharedKernel.Abstractions.Entities;

namespace Order.Api2.Entities;

/// <summary>
/// مستخدم اوردر — عميل أو تاجر. تسجيل الدخول برقم الجوال (SMS OTP).
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
    public string? AvatarUrl { get; set; }

    /// <summary>"customer" أو "vendor"</summary>
    public string Role { get; set; } = "customer";
    public bool IsActive { get; set; } = true;

    // App preferences (light/dark, ar/en)
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "ar";

    // Default car details (saved between orders)
    public string? CarModel { get; set; }
    public string? CarColor { get; set; }
    public string? CarPlate { get; set; }
}
