using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Order.Web2.Services;

/// <summary>
/// Per-circuit auth state, persisted to ProtectedLocalStorage so it survives
/// full page reloads (which kill the SignalR circuit). Pages call
/// <c>EnsureRestoredAsync</c> from their first <c>OnAfterRenderAsync</c> to
/// rehydrate from localStorage if a token is present.
/// </summary>
public class AuthStateService
{
    private const string Key = "order_auth";

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

    public event Action? OnChanged;

    /// <summary>
    /// Restore from localStorage on first call. Safe to call from any
    /// component's <c>OnAfterRenderAsync(firstRender: true)</c>.
    /// </summary>
    public async Task EnsureRestoredAsync()
    {
        if (_restored || IsAuthenticated) { _restored = true; return; }
        try
        {
            var stored = await _storage.GetAsync<StoredAuth>(Key);
            // Only mark as restored once JS interop actually succeeded.
            _restored = true;
            if (stored.Success && stored.Value is { } v && !string.IsNullOrEmpty(v.AccessToken))
            {
                UserId = v.UserId;
                PhoneNumber = v.PhoneNumber;
                FullName = v.FullName;
                AccessToken = v.AccessToken;
                OnChanged?.Invoke();
            }
        }
        catch
        {
            // JS not available yet (prerender) — leave _restored = false
            // so the next OnAfterRender call retries.
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
        catch { /* ignore */ }
        OnChanged?.Invoke();
    }

    public async Task SignOutAsync()
    {
        UserId = null;
        PhoneNumber = null;
        FullName = null;
        AccessToken = null;
        try { await _storage.DeleteAsync(Key); } catch { /* ignore */ }
        OnChanged?.Invoke();
    }

    public record StoredAuth(Guid UserId, string? PhoneNumber, string? FullName, string AccessToken);
}
