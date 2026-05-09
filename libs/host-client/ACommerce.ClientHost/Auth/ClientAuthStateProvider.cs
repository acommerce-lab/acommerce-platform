using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace ACommerce.ClientHost.Auth;

/// <summary>
/// مُزَوِّد حالة المُصادَقة Blazor عام. كلّ استدعاء يُحاول استعادة JWT من
/// <see cref="IClientAuthPersistence"/> ما لم تَنجح بَعد. SSR يُعطي "غير
/// مُصادَق" مَبدئياً ثمّ بَعد interactive bind يَنجح localStorage فيُعلِم
/// عبر NotifyAuthenticationStateChanged.
///
/// <para>الـ scheme name يُحقَن عبر <see cref="ClientAuthSchemeOptions"/>.</para>
/// </summary>
public sealed class ClientAuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly IClientAuthState _state;
    private readonly IClientAuthPersistence _persistence;
    private readonly string _scheme;

    public ClientAuthStateProvider(
        IClientAuthState state,
        IClientAuthPersistence persistence,
        ClientAuthSchemeOptions options)
    {
        _state = state;
        _persistence = persistence;
        _scheme = options.Scheme;
        _state.OnChanged += OnStateChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_persistence.RestoreCompleted.IsCompleted)
            _ = _persistence.RestoreAsync();
        await Task.WhenAny(_persistence.RestoreCompleted, Task.Delay(3000));

        var identity = _state.IsAuthenticated
            ? new ClaimsIdentity(BuildClaims(), _scheme)
            : new ClaimsIdentity();

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private IEnumerable<Claim> BuildClaims()
    {
        yield return new Claim(ClaimTypes.NameIdentifier, _state.UserId!.ToString()!);
        yield return new Claim(ClaimTypes.Name, _state.FullName ?? "user");
        if (!string.IsNullOrEmpty(_state.Role))
            yield return new Claim(ClaimTypes.Role, _state.Role!);
    }

    private void OnStateChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public void Dispose() => _state.OnChanged -= OnStateChanged;
}

/// <summary>اسم الـ scheme الذي يَظهر في <c>ClaimsIdentity.AuthenticationType</c>.</summary>
public sealed record ClientAuthSchemeOptions(string Scheme);
