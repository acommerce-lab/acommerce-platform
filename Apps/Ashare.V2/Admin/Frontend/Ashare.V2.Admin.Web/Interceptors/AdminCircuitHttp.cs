using System.Net.Http.Headers;
using Ashare.V2.Admin.Web.Store;

namespace Ashare.V2.Admin.Web.Interceptors;

public sealed class AdminCircuitHttp : IDisposable
{
    public HttpClient Client { get; }
    private readonly AppStore _store;

    public AdminCircuitHttp(IHttpClientFactory factory, AppStore store)
    {
        Client = factory.CreateClient("ashare-v2-admin");
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
