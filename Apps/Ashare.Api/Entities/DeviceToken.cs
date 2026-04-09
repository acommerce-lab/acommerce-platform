using ACommerce.SharedKernel.Abstractions.Entities;

namespace Ashare.Api.Entities;

/// <summary>
/// رمز جهاز Firebase. يخزن لكل مستخدم.
/// </summary>
public class DeviceToken : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UserId { get; set; }
    public string Token { get; set; } = default!;
    public string Platform { get; set; } = "android"; // android, ios, web
    public bool IsActive { get; set; } = true;
    public DateTime? LastSeenAt { get; set; }
}

/// <summary>
/// جلسة 2FA - تخزن التحديات النشطة (Nafath, SMS, Email).
/// </summary>
public class TwoFactorChallengeRecord : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string ChallengeId { get; set; } = default!;
    public string UserIdentifier { get; set; } = default!;
    public string ChannelName { get; set; } = default!; // sms, email, nafath
    public string Status { get; set; } = "pending";
    public string? CodeHash { get; set; }
    public string? ExternalId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
}
