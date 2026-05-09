using ACommerce.Client.Operations;
using ACommerce.ClientHost.Auth;
using ACommerce.ClientHost.Preferences;

namespace ACommerce.ClientHost;

/// <summary>
/// Adapter يَكشِف <see cref="IClientAuthState"/> + <see cref="IUiPreferences"/>
/// كَ <see cref="ITemplateStore"/>. تَستَهلِكه قَوالِب Marketplace
/// (AcMarketplaceHomePage، AcListingExplorePage، …) التي تَحتاج userId
/// + theme + language لِبِناء OAM ops داخِليّاً.
///
/// <para>التَطبيق يُسَجِّله في DI:
/// <code>services.AddScoped&lt;ITemplateStore, ClientStateTemplateStore&gt;();</code>
/// ثُمّ يَحقن كلّ widget يَحتاج <c>ITemplateStore</c> هذا الـ adapter.
/// </para>
///
/// <para>Phase A→F: V1's AppStore يُحَقِّق ITemplateStore مُباشَرَةً ⇒ هذا
/// الـ adapter يَحلّ مَحَلَّه عِند حَذف AppStore (Phase F).</para>
/// </summary>
public sealed class ClientStateTemplateStore : ITemplateStore, IDisposable
{
    private readonly IClientAuthState _auth;
    private readonly IUiPreferences _prefs;

    public ClientStateTemplateStore(IClientAuthState auth, IUiPreferences prefs)
    {
        _auth  = auth;
        _prefs = prefs;
        _auth.OnChanged  += FireChanged;
        _prefs.OnChanged += FireChanged;
    }

    public bool    IsAuthenticated => _auth.IsAuthenticated;
    public Guid?   UserId          => _auth.UserId;
    public string? AccessToken     => _auth.AccessToken;
    public string  Theme           => _prefs.Theme;
    public string  Language        => _prefs.Language;

    public event Action? OnChanged;
    private void FireChanged() => OnChanged?.Invoke();

    public void Dispose()
    {
        _auth.OnChanged  -= FireChanged;
        _prefs.OnChanged -= FireChanged;
    }
}
