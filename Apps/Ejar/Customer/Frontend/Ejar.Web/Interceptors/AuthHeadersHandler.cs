using System.Net.Http.Headers;
using Ejar.Web.Store;

namespace Ejar.Web.Interceptors;

public sealed class AuthHeadersHandler : DelegatingHandler
{
    private readonly AppStore _store;
    public AuthHeadersHandler(AppStore store) => _store = store;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_store.Auth.AccessToken))
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _store.Auth.AccessToken);
        return base.SendAsync(request, cancellationToken);
    }
}
