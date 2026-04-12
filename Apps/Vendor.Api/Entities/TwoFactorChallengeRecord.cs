using ACommerce.SharedKernel.Abstractions.Entities;

namespace Vendor.Api.Entities;

/// <summary>
/// جلسة 2FA — تخزن تحديات SMS النشطة لـ Vendor.Api.
/// </summary>
public class TwoFactorChallengeRecord : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string ChallengeId { get; set; } = default!;
    public string UserIdentifier { get; set; } = default!;
    public string ChannelName { get; set; } = default!;
    public string Status { get; set; } = "pending";
    public string? CodeHash { get; set; }
    public string? ExternalId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
}
