namespace ACommerce.Authentication.Operations.Abstractions;

/// <summary>
/// بيانات اعتماد - كائن مبهم يُمرر للمُصادق.
/// المطور يعرف محتواه: UsernamePassword, Token, Certificate, etc.
/// </summary>
public interface ICredential
{
    /// <summary>نوع بيانات الاعتماد: "password", "token", "nafath", "otp"</summary>
    string CredentialType { get; }
}

/// <summary>
/// هوية (Principal) - نتيجة المصادقة الناجحة.
/// لا كيان - المطور يُطبقها على كيانه.
/// </summary>
public interface IPrincipal
{
    string UserId { get; }
    string? DisplayName { get; }
    IReadOnlyDictionary<string, string> Claims { get; }
}

/// <summary>
/// المُصادق - الواجهة الرئيسية.
/// كل مزود (Token, Password, Nafath, OAuth, etc.) يطبقها.
/// </summary>
public interface IAuthenticator
{
    /// <summary>اسم المزود: "token", "password", "nafath"</summary>
    string Name { get; }

    /// <summary>نوع بيانات الاعتماد المدعومة</summary>
    string SupportedCredentialType { get; }

    /// <summary>
    /// التحقق من بيانات الاعتماد وإرجاع الهوية.
    /// يرمي AuthenticationException عند الفشل.
    /// </summary>
    Task<IPrincipal> AuthenticateAsync(ICredential credential, CancellationToken ct = default);
}

/// <summary>
/// مصدر الرموز (Token Issuer).
/// يُطبقه المزود الذي يُنشئ tokens بعد المصادقة.
/// </summary>
public interface ITokenIssuer
{
    /// <summary>إنشاء token للهوية</summary>
    Task<AuthToken> IssueAsync(IPrincipal principal, CancellationToken ct = default);

    /// <summary>تجديد token</summary>
    Task<AuthToken> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>إبطال token</summary>
    Task RevokeAsync(string token, CancellationToken ct = default);
}

/// <summary>
/// مخزن الجلسات - لا كيان، مجرد عقد.
/// </summary>
public interface ISessionStore
{
    Task CreateAsync(AuthSession session, CancellationToken ct = default);
    Task<AuthSession?> GetAsync(string sessionId, CancellationToken ct = default);
    Task RemoveAsync(string sessionId, CancellationToken ct = default);
    Task<IEnumerable<AuthSession>> GetByUserAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// استثناء المصادقة
/// </summary>
public class AuthenticationException : Exception
{
    public string Reason { get; }
    public AuthenticationException(string reason, string message) : base(message) => Reason = reason;
}
