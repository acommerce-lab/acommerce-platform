using System.Net.Http.Headers;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Interceptors;

public sealed class CultureHeadersHandler : DelegatingHandler
{
    private readonly AppStore _store;
    public CultureHeadersHandler(AppStore store) => _store = store;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var c = _store.Ui.Culture;
        request.Headers.AcceptLanguage.Clear();
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(c.Language));
        request.Headers.Remove("X-User-Timezone");
        request.Headers.Add("X-User-Timezone", c.TimeZone);
        request.Headers.Remove("X-User-Currency");
        request.Headers.Add("X-User-Currency", c.Currency);
        return base.SendAsync(request, cancellationToken);
    }
}
