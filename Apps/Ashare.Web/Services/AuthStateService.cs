namespace Ashare.Web.Services;

/// <summary>
/// خدمة Scoped تحفظ حالة مصادقة المستخدم الحالية.
/// تُستخدم في Blazor components عبر injection.
/// </summary>
public class AuthStateService
{
    public Guid? UserId { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public string Language { get; set; } = "ar";

    public bool IsAuthenticated => UserId.HasValue && !string.IsNullOrEmpty(AccessToken);

    public event Action? OnAuthStateChanged;

    public void SignIn(Guid userId, string phoneNumber, string accessToken, string refreshToken, DateTimeOffset expiresAt)
    {
        UserId = userId;
        PhoneNumber = phoneNumber;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        ExpiresAt = expiresAt;
        OnAuthStateChanged?.Invoke();
    }

    public void SignOut()
    {
        UserId = null;
        PhoneNumber = null;
        AccessToken = null;
        RefreshToken = null;
        ExpiresAt = null;
        OnAuthStateChanged?.Invoke();
    }
}
