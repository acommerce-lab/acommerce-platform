using ACommerce.OperationEngine.Core;

namespace ACommerce.Kits.Profiles.Operations;

/// <summary>
/// عقد profile المستخدم. مستقلّ عن Auth.Operations:
/// <list type="bullet">
///   <item><c>Auth</c> = identity + tokens (stateless).</item>
///   <item><c>Profiles</c> = mutable user metadata.</item>
/// </list>
/// التطبيق يُلصِقه على كيان DB (مثلاً <c>UserEntity</c> في إيجار).
/// </summary>
public interface IUserProfile
{
    string  Id { get; }
    string  FullName { get; }
    string  Phone { get; }
    bool    PhoneVerified { get; }
    string? Email { get; }
    bool    EmailVerified { get; }
    string  City { get; }
    string? AvatarUrl { get; }
    DateTime MemberSince { get; }
}

/// <summary>POCO impl — يُسلَّم من ProfilesController في GET، ويُبنى في PUT.</summary>
public sealed record InMemoryUserProfile(
    string   Id,
    string   FullName,
    string   Phone,
    bool     PhoneVerified,
    string?  Email,
    bool     EmailVerified,
    string   City,
    string?  AvatarUrl,
    DateTime MemberSince
) : IUserProfile;

public static class ProfileOps
{
    public static readonly OperationType Update = new("profile.update");
}

public static class ProfileTagKeys
{
    public static readonly TagKey UserId = new("profile_user_id");
}
