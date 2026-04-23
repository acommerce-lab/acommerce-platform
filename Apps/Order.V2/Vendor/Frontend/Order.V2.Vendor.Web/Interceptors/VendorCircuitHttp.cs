using System.Net.Http.Headers;
using Order.V2.Vendor.Web.Store;

namespace Order.V2.Vendor.Web.Interceptors;

public sealed class VendorCircuitHttp : IDisposable
{
    private readonly AppStore _store;
    public HttpClient Client { get; }

    public VendorCircuitHttp(IHttpClientFactory factory, AppStore store)
    {
        _store  = store;
        Client  = factory.CreateClient("order-v2-vendor");
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
