using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Ashare.V2.Admin.Web.Services;

public class AuthStateService
{
    private readonly ProtectedLocalStorage _storage;
    private const string Key = "ashare_v2_admin_auth";

    public Guid?   UserId      { get; private set; }
    public string? FullName    { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? AccessToken { get; private set; }
    public bool    IsAuthenticated => UserId.HasValue && !string.IsNullOrEmpty(AccessToken);

    public AuthStateService(ProtectedLocalStorage storage) => _storage = storage;

    public async Task EnsureRestoredAsync()
    {
        try
        {
            var r = await _storage.GetAsync<AuthSnapshot>(Key);
            if (r.Success && r.Value is { } snap)
            {
                UserId = snap.UserId; FullName = snap.FullName;
                PhoneNumber = snap.PhoneNumber; AccessToken = snap.AccessToken;
            }
        }
        catch { }
    }

    public async Task SignInAsync(Guid userId, string? fullName, string? phone, string accessToken)
    {
        UserId = userId; FullName = fullName; PhoneNumber = phone; AccessToken = accessToken;
        try { await _storage.SetAsync(Key, new AuthSnapshot(userId, fullName, phone, accessToken)); } catch { }
    }

    public async Task SignOutAsync()
    {
        UserId = null; FullName = null; PhoneNumber = null; AccessToken = null;
        try { await _storage.DeleteAsync(Key); } catch { }
    }

    private record AuthSnapshot(Guid UserId, string? FullName, string? PhoneNumber, string? AccessToken);
}
