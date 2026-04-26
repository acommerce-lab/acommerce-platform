using ACommerce.OperationEngine.Core;
namespace ACommerce.Authentication.Operations.Abstractions;

/// <summary>
/// حالة المصادقة - كائن بدل نص.
/// </summary>
public sealed class AuthStatus
{
    public string Value { get; }
    private AuthStatus(string value) => Value = value;

    public static readonly AuthStatus Pending = new("pending");
    public static readonly AuthStatus Authenticated = new("authenticated");
    public static readonly AuthStatus Rejected = new("rejected");
    public static readonly AuthStatus Expired = new("expired");
    public static readonly AuthStatus Revoked = new("revoked");
    public static readonly AuthStatus Locked = new("locked");

    public static AuthStatus Custom(string v) => new(v);
    public override string ToString() => Value;
    public static implicit operator string(AuthStatus s) => s.Value;
}

/// <summary>
/// نوع بيانات الاعتماد - كائن بدل نص.
/// </summary>
public sealed class CredentialType
{
    public string Value { get; }
    private CredentialType(string value) => Value = value;

    public static readonly CredentialType Password = new("password");
    public static readonly CredentialType Token = new("token");
    public static readonly CredentialType RefreshToken = new("refresh_token");
    public static readonly CredentialType Certificate = new("certificate");
    public static readonly CredentialType ApiKey = new("api_key");
    public static readonly CredentialType Nafath = new("nafath");
    public static readonly CredentialType Otp = new("otp");
    public static readonly CredentialType Biometric = new("biometric");

    public static CredentialType Custom(string v) => new(v);
    public override string ToString() => Value;
    public static implicit operator string(CredentialType ct) => ct.Value;
}

/// <summary>
/// رمز المصادقة الصادر.
/// </summary>
public record AuthToken(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAt,
    string TokenType = "Bearer")
{
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}

/// <summary>
/// جلسة مصادقة.
/// </summary>
public record AuthSession(
    string SessionId,
    string UserId,
    string AuthenticatorName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}

/// <summary>
/// مفاتيح علامات المصادقة - المفاتيح ثابتة، القيم حرة.
/// </summary>
public static class AuthTags
{
    /// <summary>اسم المُصادق. القيم: "token", "password", "nafath"</summary>
    public static readonly TagKey Authenticator = new("authenticator");

    /// <summary>نوع بيانات الاعتماد. القيم: "password", "token", ...</summary>
    public static readonly TagKey Credential = new("credential");

    /// <summary>حالة المصادقة. القيم: "pending", "authenticated", ...</summary>
    public static readonly TagKey Status = new("auth_status");

    /// <summary>معرف الجلسة</summary>
    public static readonly TagKey Session = new("session_id");

    /// <summary>نوع الرمز. القيم: "access", "refresh", "id"</summary>
    public static readonly TagKey TokenKind = new("token_kind");

    /// <summary>دور الطرف. القيم: "subject" (المستخدم), "issuer" (المصدر)</summary>
    public static readonly TagKey Role = new("role");

    /// <summary>سبب الرفض. القيم: "invalid_credential", "expired", "locked"</summary>
    public static readonly TagKey Reason = new("reason");
}

/// <summary>
/// هوية الطرف في عمليات المصادقة.
/// </summary>
public sealed class AuthPartyId
{
    public string Type { get; }
    public string Id { get; }
    public string FullId { get; }

    private AuthPartyId(string type, string id)
    {
        Type = type; Id = id; FullId = $"{type}:{id}";
    }

    public static AuthPartyId User(string userId) => new("User", userId);
    public static AuthPartyId Issuer(string name) => new("Issuer", name);
    public static AuthPartyId Session(string sessionId) => new("Session", sessionId);
    public static AuthPartyId System => new("System", "");

    public override string ToString() => string.IsNullOrEmpty(Id) ? Type : FullId;
    public static implicit operator string(AuthPartyId pid) => pid.ToString();
}
