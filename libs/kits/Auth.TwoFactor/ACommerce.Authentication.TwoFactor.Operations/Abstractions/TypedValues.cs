using ACommerce.OperationEngine.Core;
namespace ACommerce.Authentication.TwoFactor.Operations.Abstractions;

/// <summary>
/// حالة التحدي.
/// </summary>
public sealed class ChallengeStatus
{
    public string Value { get; }
    private ChallengeStatus(string value) => Value = value;

    public static readonly ChallengeStatus Initiated = new("initiated");
    public static readonly ChallengeStatus Pending = new("pending");
    public static readonly ChallengeStatus Verified = new("verified");
    public static readonly ChallengeStatus Failed = new("failed");
    public static readonly ChallengeStatus Expired = new("expired");
    public static readonly ChallengeStatus Cancelled = new("cancelled");

    public static ChallengeStatus Custom(string v) => new(v);
    public override string ToString() => Value;
    public static implicit operator string(ChallengeStatus s) => s.Value;
}

/// <summary>
/// تحدي المصادقة الثنائية.
/// </summary>
public class Challenge
{
    public required string Id { get; init; }
    public required string UserIdentifier { get; init; }
    public required string ChannelName { get; init; }
    public ChallengeStatus Status { get; set; } = ChallengeStatus.Initiated;

    /// <summary>الكود المُولّد (لقنوات GeneratesCode = true). مخزّن hashed غالباً.</summary>
    public string? CodeHash { get; set; }

    /// <summary>معرف خارجي عند المزود (Nafath transId مثلاً)</summary>
    public string? ExternalId { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.AddMinutes(5);
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; init; } = 3;

    public Dictionary<string, string> Metadata { get; init; } = new();

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool ExceededAttempts => AttemptCount >= MaxAttempts;
}

/// <summary>
/// مفاتيح علامات المصادقة الثنائية.
/// </summary>
public static class TwoFactorTags
{
    /// <summary>اسم القناة. القيم: "sms", "email", "nafath", "totp"</summary>
    public static readonly TagKey Channel = new("tfa_channel");

    /// <summary>حالة التحدي. القيم: "initiated", "verified", "failed", ...</summary>
    public static readonly TagKey Status = new("tfa_status");

    /// <summary>معرف التحدي</summary>
    public static readonly TagKey Challenge = new("challenge_id");

    /// <summary>المعرف الخارجي عند المزود</summary>
    public static readonly TagKey ExternalId = new("external_id");

    /// <summary>دور الطرف. القيم: "subject" (المستخدم), "channel" (القناة)</summary>
    public static readonly TagKey Role = new("role");

    /// <summary>سبب الفشل. القيم: "wrong_code", "expired", "too_many_attempts"</summary>
    public static readonly TagKey Reason = new("reason");
}

/// <summary>
/// هوية الطرف في عمليات 2FA.
/// </summary>
public sealed class TwoFactorPartyId
{
    public string Type { get; }
    public string Id { get; }
    public string FullId { get; }

    private TwoFactorPartyId(string type, string id)
    {
        Type = type; Id = id; FullId = $"{type}:{id}";
    }

    public static TwoFactorPartyId User(string userId) => new("User", userId);
    public static TwoFactorPartyId Channel(string channelName) => new("Channel", channelName);
    public static TwoFactorPartyId Challenge(string challengeId) => new("Challenge", challengeId);
    public static TwoFactorPartyId System => new("System", "");

    public override string ToString() => string.IsNullOrEmpty(Id) ? Type : FullId;
    public static implicit operator string(TwoFactorPartyId pid) => pid.ToString();
}
