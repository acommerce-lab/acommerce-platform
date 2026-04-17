using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Vendor.Web.Services;

/// <summary>
/// Per-circuit auth state, persisted to ProtectedLocalStorage so it survives
/// full page reloads (which kill the SignalR circuit).
/// </summary>
public class AuthStateService
{
    private const string Key = "vendor_auth";

    private readonly ProtectedLocalStorage _storage;
    private bool _restored;

    public AuthStateService(ProtectedLocalStorage storage)
    {
        _storage = storage;
    }

    public Guid? UserId { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? FullName { get; private set; }
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
                FullName = v.FullName;
                AccessToken = v.AccessToken;
            }
        }
        catch
        {
            // JS not available yet (prerender) — leave _restored = false
        }
    }

    public async Task SignInAsync(Guid userId, string? phoneNumber, string? fullName, string accessToken)
    {
        UserId = userId;
        PhoneNumber = phoneNumber;
        FullName = fullName;
        AccessToken = accessToken;
        try
        {
            await _storage.SetAsync(Key, new StoredAuth(userId, phoneNumber, fullName, accessToken));
        }
        catch { }
    }

    public async Task SignOutAsync()
    {
        UserId = null;
        PhoneNumber = null;
        FullName = null;
        AccessToken = null;
        try { await _storage.DeleteAsync(Key); } catch { }
    }

    public record StoredAuth(Guid UserId, string? PhoneNumber, string? FullName, string AccessToken);
}
