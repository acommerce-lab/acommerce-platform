using Ashare.V2.Provider.Web.Store;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Ashare.V2.Provider.Web.Services;

public class AuthStateService
{
    private readonly ProtectedLocalStorage _storage;
    private readonly AppStore _store;
    private bool _restored;

    public AuthStateService(ProtectedLocalStorage storage, AppStore store)
    {
        _storage = storage;
        _store   = store;
    }

    public async Task EnsureRestoredAsync()
    {
        if (_restored) return;
        _restored = true;
        try
        {
            var uid   = await _storage.GetAsync<string>("provider_uid");
            var name  = await _storage.GetAsync<string>("provider_name");
            var token = await _storage.GetAsync<string>("provider_token");
            var nid   = await _storage.GetAsync<string>("provider_nid");

            if (uid.Success && token.Success && Guid.TryParse(uid.Value, out var id))
            {
                _store.Auth.UserId      = id;
                _store.Auth.FullName    = name.Value;
                _store.Auth.AccessToken = token.Value;
                _store.Auth.NationalId  = nid.Value;
                _store.NotifyChanged();
            }
        }
        catch { /* fresh session */ }
    }

    public async Task PersistAsync()
    {
        if (!_store.Auth.IsAuthenticated) { await ClearAsync(); return; }
        await _storage.SetAsync("provider_uid",   _store.Auth.UserId!.Value.ToString());
        await _storage.SetAsync("provider_name",  _store.Auth.FullName ?? "");
        await _storage.SetAsync("provider_token", _store.Auth.AccessToken ?? "");
        await _storage.SetAsync("provider_nid",   _store.Auth.NationalId ?? "");
    }

    public async Task ClearAsync()
    {
        await _storage.DeleteAsync("provider_uid");
        await _storage.DeleteAsync("provider_name");
        await _storage.DeleteAsync("provider_token");
        await _storage.DeleteAsync("provider_nid");
    }
}
