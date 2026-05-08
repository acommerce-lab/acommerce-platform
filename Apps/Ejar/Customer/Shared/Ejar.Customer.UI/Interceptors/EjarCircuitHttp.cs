using System.Net.Http.Headers;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Interceptors;

/// <summary>
/// Holds the single HttpClient for this Blazor circuit and keeps the
/// Authorization header in sync with AppStore.Auth.AccessToken.
///
/// Why a dedicated scoped service instead of a DelegatingHandler:
/// IHttpClientFactory resolves DelegatingHandlers from its own internal DI
/// scope, not the circuit scope, so injecting AppStore into a handler gives
/// a fresh empty instance — the token written after login is never visible.
/// This class is Scoped (one per circuit), receives the correct AppStore, and
/// subscribes to OnChanged so the header is updated the moment the token is set.
/// </summary>
public sealed class EjarCircuitHttp : IDisposable
{
    public HttpClient Client { get; }
    private readonly AppStore _store;

    public EjarCircuitHttp(IHttpClientFactory factory, AppStore store)
    {
        Client = factory.CreateClient("ejar");
        _store = store;
        _store.OnChanged += SyncToken;
        SyncToken();
    }

    private void SyncToken()
    {
        var token = _store.Auth.AccessToken;
        Client.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public void Dispose() => _store.OnChanged -= SyncToken;
}
