using System.Net.Http.Headers;
using Ashare.V2.Provider.Web.Store;

namespace Ashare.V2.Provider.Web.Interceptors;

public sealed class ProviderCircuitHttp : IDisposable
{
    private readonly AppStore _store;
    public HttpClient Client { get; }

    public ProviderCircuitHttp(IHttpClientFactory factory, AppStore store)
    {
        _store  = store;
        Client  = factory.CreateClient("ashare-v2");
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
