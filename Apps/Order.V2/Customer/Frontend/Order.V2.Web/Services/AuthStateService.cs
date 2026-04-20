using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Order.V2.Web.Services;

public class AuthStateService
{
    private const string Key = "order_v2_auth";

    private readonly ProtectedLocalStorage _storage;
    private bool _restored;

    public AuthStateService(ProtectedLocalStorage storage) => _storage = storage;

    public Guid? UserId { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? FullName { get; private set; }
    public string? AccessToken { get; private set; }

    public bool IsAuthenticated => UserId.HasValue && !string.IsNullOrEmpty(AccessToken);

    public event Action? OnChanged;

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
                OnChanged?.Invoke();
            }
        }
        catch { }
    }

    public async Task SignInAsync(Guid userId, string? phoneNumber, string? fullName, string accessToken)
    {
        UserId = userId; PhoneNumber = phoneNumber; FullName = fullName; AccessToken = accessToken;
        try { await _storage.SetAsync(Key, new StoredAuth(userId, phoneNumber, fullName, accessToken)); } catch { }
        OnChanged?.Invoke();
    }

    public async Task SignOutAsync()
    {
        UserId = null; PhoneNumber = null; FullName = null; AccessToken = null;
        try { await _storage.DeleteAsync(Key); } catch { }
        OnChanged?.Invoke();
    }

    public record StoredAuth(Guid UserId, string? PhoneNumber, string? FullName, string AccessToken);
}
