using System.Net.Http.Headers;
using Order.V2.Admin.Web.Store;

namespace Order.V2.Admin.Web.Interceptors;

public sealed class AdminCircuitHttp : IDisposable
{
    private readonly AppStore _store;
    public HttpClient Client { get; }

    public AdminCircuitHttp(IHttpClientFactory factory, AppStore store)
    {
        _store  = store;
        Client  = factory.CreateClient("order-v2-admin");
        _store.OnChanged += Sync;
        Sync();
    }

    private void Sync()
    {
        if (_store.Auth.IsAuthenticated)
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _store.Auth.AccessToken);
        else
            Client.DefaultRequestHeaders.Authorization = null;
    }

    public void Dispose() => _store.OnChanged -= Sync;
}
