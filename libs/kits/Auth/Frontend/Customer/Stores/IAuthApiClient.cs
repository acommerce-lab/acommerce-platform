namespace ACommerce.Kits.Auth.Frontend.Customer.Stores;

/// <summary>
/// عميل HTTP خاصّ بـ Auth kit. يَعرف شكل ردّ <c>AuthController</c>:
/// <list type="bullet">
///   <item><c>POST /auth/otp/request</c> ⇒ <c>{ masked, expiresInSeconds }</c></item>
///   <item><c>POST /auth/otp/verify</c> ⇒ <c>{ token, userId, name, phone, role }</c></item>
///   <item><c>POST /auth/logout</c></item>
/// </list>
/// </summary>
public interface IAuthApiClient
{
    Task<AuthRequestResult> RequestOtpAsync(string phone, CancellationToken ct = default);
    Task<AuthVerifyResult>  VerifyOtpAsync(string phone, string code, CancellationToken ct = default);
    Task                    LogoutAsync(CancellationToken ct = default);
}

public readonly record struct AuthRequestResult(bool Success, string? Error);

public readonly record struct AuthVerifyResult(
    bool    Success,
    string? Token,
    string? UserId,
    string? FullName,
    string? Phone,
    string? Role,
    string? Error);
