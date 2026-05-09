using System.Net.Http.Headers;

namespace ACommerce.ClientHost.Auth;

/// <summary>
/// HttpClient مُتَزامِن مع <see cref="IClientAuthState.AccessToken"/>. كلّ
/// kit api client يَستهلك هذا الـ HttpClient عبر <c>KitHttpClient</c> فيَحصل
/// على Bearer تلقائياً بَعد أيّ تَغيير على Auth state (login/logout).
///
/// <para>اسم الـ HttpClient يُحقَن عبر <see cref="AuthenticatedHttpClientOptions"/>
/// (مَفتاح <c>IHttpClientFactory.CreateClient</c>).</para>
/// </summary>
public sealed class AuthenticatedHttpClient : IDisposable
{
    public HttpClient Client { get; }
    private readonly IClientAuthState _state;

    public AuthenticatedHttpClient(
        IHttpClientFactory factory,
        IClientAuthState state,
        AuthenticatedHttpClientOptions options)
    {
        Client = factory.CreateClient(options.HttpClientName);
        _state = state;
        _state.OnChanged += SyncToken;
        SyncToken();
    }

    private void SyncToken()
    {
        var token = _state.AccessToken;
        Client.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public void Dispose() => _state.OnChanged -= SyncToken;
}

/// <summary>إعدادات الـ AuthenticatedHttpClient — اسم الـ logical HttpClient.</summary>
public sealed record AuthenticatedHttpClientOptions(string HttpClientName);
