using ACommerce.Authentication.Operations.Abstractions;
using ACommerce.Authentication.Operations.Operations;
using ACommerce.OperationEngine.Core;

namespace ACommerce.Authentication.Operations;

/// <summary>
/// تهيئة المصادقة.
///
/// services.AddAuth(config => {
///     config.AddAuthenticator(new TokenAuthenticator(validator));
///     config.UseIssuer(new JwtTokenIssuer(...));
///     config.UseSessionStore(new InMemorySessionStore());
/// });
/// </summary>
public class AuthConfig
{
    internal Dictionary<string, IAuthenticator> Authenticators { get; } = new();
    internal ITokenIssuer? Issuer { get; private set; }
    internal ISessionStore? SessionStore { get; private set; }

    public AuthConfig AddAuthenticator(IAuthenticator authenticator)
    {
        Authenticators[authenticator.Name] = authenticator;
        return this;
    }

    public AuthConfig UseIssuer(ITokenIssuer issuer)
    {
        Issuer = issuer;
        return this;
    }

    public AuthConfig UseSessionStore(ISessionStore store)
    {
        SessionStore = store;
        return this;
    }
}

/// <summary>
/// واجهة المطور البسيطة للمصادقة.
///
///   var result = await authService.SignInAsync("token", new TokenCredential(jwt));
///   if (result.Succeeded && result.Token != null) { ... }
/// </summary>
public class AuthService
{
    private readonly AuthConfig _config;
    private readonly OpEngine _engine;

    public AuthService(AuthConfig config, OpEngine engine)
    {
        _config = config;
        _engine = engine;
    }

    /// <summary>تسجيل دخول عبر مُصادق مسجّل</summary>
    public async Task<AuthResult> SignInAsync(
        string authenticatorName,
        ICredential credential,
        CancellationToken ct = default)
    {
        if (!_config.Authenticators.TryGetValue(authenticatorName, out var authenticator))
            throw new ArgumentException($"Authenticator '{authenticatorName}' not registered.");

        // نبدأ بـ placeholder للمستخدم لأنه غير معروف قبل المصادقة
        var user = AuthPartyId.User("pending");
        var op = AuthOps.SignIn(user, credential, authenticator, _config.Issuer);

        var result = await _engine.ExecuteAsync(op, ct);

        if (!result.Success)
        {
            return new AuthResult(
                Succeeded: false,
                Reason: result.Context!.TryGet<string>("error", out var err) ? err : "authentication_failed",
                Principal: null,
                Token: null);
        }

        result.Context!.TryGet<IPrincipal>("principal", out var principal);
        result.Context!.TryGet<AuthToken>("token", out var token);

        // تخزين الجلسة
        if (_config.SessionStore != null && principal != null)
        {
            var session = new AuthSession(
                SessionId: Guid.NewGuid().ToString(),
                UserId: principal.UserId,
                AuthenticatorName: authenticator.Name,
                CreatedAt: DateTimeOffset.UtcNow,
                ExpiresAt: token?.ExpiresAt ?? DateTimeOffset.UtcNow.AddHours(1));

            await _config.SessionStore.CreateAsync(session, ct);
        }

        return new AuthResult(Succeeded: true, Reason: null, Principal: principal, Token: token);
    }

    /// <summary>تسجيل خروج</summary>
    public async Task SignOutAsync(string userId, string sessionOrToken, CancellationToken ct = default)
    {
        var op = AuthOps.SignOut(AuthPartyId.User(userId), sessionOrToken, _config.Issuer, _config.SessionStore);
        await _engine.ExecuteAsync(op, ct);
    }

    /// <summary>تجديد الرمز</summary>
    public async Task<AuthResult> RefreshAsync(string userId, string refreshToken, CancellationToken ct = default)
    {
        if (_config.Issuer == null)
            throw new InvalidOperationException("No token issuer configured. Call config.UseIssuer().");

        var op = AuthOps.RefreshToken(AuthPartyId.User(userId), refreshToken, _config.Issuer);
        var result = await _engine.ExecuteAsync(op, ct);

        if (!result.Success)
            return new AuthResult(Succeeded: false, Reason: "refresh_failed", Principal: null, Token: null);

        result.Context!.TryGet<AuthToken>("token", out var token);
        return new AuthResult(Succeeded: true, Reason: null, Principal: null, Token: token);
    }

    /// <summary>التحقق من بيانات اعتماد دون إنشاء جلسة</summary>
    public async Task<AuthResult> ValidateAsync(
        string authenticatorName,
        ICredential credential,
        CancellationToken ct = default)
    {
        if (!_config.Authenticators.TryGetValue(authenticatorName, out var authenticator))
            throw new ArgumentException($"Authenticator '{authenticatorName}' not registered.");

        var op = AuthOps.Validate(credential, authenticator);
        var result = await _engine.ExecuteAsync(op, ct);

        result.Context!.TryGet<bool>("valid", out var valid);
        result.Context!.TryGet<IPrincipal>("principal", out var principal);
        result.Context!.TryGet<string>("reason", out var reason);

        return new AuthResult(
            Succeeded: valid && result.Success,
            Reason: reason,
            Principal: principal,
            Token: null);
    }
}

/// <summary>نتيجة عملية مصادقة</summary>
public record AuthResult(
    bool Succeeded,
    string? Reason,
    IPrincipal? Principal,
    AuthToken? Token);
