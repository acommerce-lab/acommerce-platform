namespace ACommerce.Kits.Auth.Frontend.Customer.Stores;

/// <summary>
/// store reactive لحالة الـ Auth على العميل. التطبيق يَربطه بمصدر JWT
/// (storage محلّيّ + refresh token). صفحات الـ Auth kit تَستهلك هذه
/// الواجهة فقط — لا تَلمس JS storage مباشرةً.
/// </summary>
public interface IAuthStore
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? FullName { get; }
    bool IsBusy { get; }
    string? LastError { get; }
    event Action? Changed;

    /// <summary>يَطلب OTP للهاتف. يَنجح ⇒ <see cref="LastError"/> = null.</summary>
    Task RequestOtpAsync(string phone, CancellationToken ct = default);

    /// <summary>يتحقّق من OTP ويَستلم JWT. يَنجح ⇒ <see cref="IsAuthenticated"/> = true.</summary>
    Task VerifyOtpAsync(string phone, string code, CancellationToken ct = default);

    Task LogoutAsync(CancellationToken ct = default);
}
