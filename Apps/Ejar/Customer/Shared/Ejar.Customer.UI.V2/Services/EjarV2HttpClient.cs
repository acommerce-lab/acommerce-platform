using System.Net.Http.Headers;

namespace Ejar.Customer.UI.V2.Services;

/// <summary>
/// HttpClient واحد لكلّ circuit مُتَزامِن مع
/// <see cref="EjarV2AppStore.Auth.AccessToken"/>. كلّ kit api client
/// يَستهلك هذا الـ HttpClient عبر <c>KitHttpClient</c> فيَحصل على Bearer
/// تلقائياً بَعد أيّ تَغيير على Auth state (login/logout).
/// </summary>
public sealed class EjarV2HttpClient : IDisposable
{
    public HttpClient Client { get; }
    private readonly EjarV2AppStore _store;

    public EjarV2HttpClient(IHttpClientFactory factory, EjarV2AppStore store)
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
