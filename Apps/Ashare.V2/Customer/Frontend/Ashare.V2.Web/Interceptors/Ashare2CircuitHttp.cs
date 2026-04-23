using System.Net.Http.Headers;
using Ashare.V2.Web.Store;

namespace Ashare.V2.Web.Interceptors;

/// <summary>
/// Circuit-scoped HttpClient holder for Ashare.V2.
/// Keeps Authorization header in sync with AppStore.Auth.AccessToken.
/// See EjarCircuitHttp for the full rationale.
/// </summary>
public sealed class Ashare2CircuitHttp : IDisposable
{
    public HttpClient Client { get; }
    private readonly AppStore _store;

    public Ashare2CircuitHttp(IHttpClientFactory factory, AppStore store)
    {
        Client = factory.CreateClient("ashare-v2");
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
