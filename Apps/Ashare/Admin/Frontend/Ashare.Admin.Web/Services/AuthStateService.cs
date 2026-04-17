using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Ashare.Admin.Web.Services;

/// <summary>
/// يُخزّن حالة الدخول في ProtectedLocalStorage حتى تبقى بعد إعادة تحميل الصفحة.
/// يُستعاد في MainLayout أول عرض، ثم يُكتب إليه عند تغيّر Store.Auth.
/// </summary>
public class AuthStateService
{
    private const string Key = "ashare_admin_auth";

    private readonly ProtectedLocalStorage _storage;
    private bool _restored;

    public AuthStateService(ProtectedLocalStorage storage)
    {
        _storage = storage;
    }

    public Guid? UserId { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? DisplayName { get; private set; }
    public string? AccessToken { get; private set; }

    public bool IsAuthenticated => UserId.HasValue && !string.IsNullOrEmpty(AccessToken);

    public async Task EnsureRestoredAsync()
    {
        if (_restored || IsAuthenticated) { _restored = true; return; }
        try
        {
            var stored = await _storage.GetAsync<StoredAuth>(Key);
            _restored = true;
            if (stored.Success && stored.Value is { } v && !string.IsNullOrEmpty(v.AccessToken))
            {
                UserId = v.UserId;
                PhoneNumber = v.PhoneNumber;
                DisplayName = v.DisplayName;
                AccessToken = v.AccessToken;
            }
        }
        catch { }
    }

    public async Task SignInAsync(Guid userId, string? phoneNumber, string? displayName, string accessToken)
    {
        UserId = userId;
        PhoneNumber = phoneNumber;
        DisplayName = displayName;
        AccessToken = accessToken;
        try
        {
            await _storage.SetAsync(Key, new StoredAuth(userId, phoneNumber, displayName, accessToken));
        }
        catch { }
    }

    public async Task SignOutAsync()
    {
        UserId = null;
        PhoneNumber = null;
        DisplayName = null;
        AccessToken = null;
        try { await _storage.DeleteAsync(Key); } catch { }
    }

    public record StoredAuth(Guid UserId, string? PhoneNumber, string? DisplayName, string AccessToken);
}
