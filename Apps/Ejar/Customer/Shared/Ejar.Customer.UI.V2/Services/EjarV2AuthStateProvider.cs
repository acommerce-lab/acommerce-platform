using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Ejar.Customer.UI.V2.Services;

/// <summary>
/// مُزَوِّد حالة المُصادَقة. كلّ استدعاء يُحاول استعادة JWT من
/// <see cref="EjarV2Persistence"/> ما لم تَنجح بَعد. SSR يُعطي "غير
/// مُصادَق" مَبدئياً ثمّ بَعد interactive bind يَنجح localStorage فيُعلِم
/// عبر NotifyAuthenticationStateChanged.
/// </summary>
public sealed class EjarV2AuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly EjarV2AppStore _store;
    private readonly EjarV2Persistence _persistence;

    public EjarV2AuthStateProvider(EjarV2AppStore store, EjarV2Persistence persistence)
    {
        _store = store;
        _persistence = persistence;
        _store.OnChanged += OnStoreChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_persistence.RestoreCompleted.IsCompleted)
            _ = _persistence.RestoreAsync();
        await Task.WhenAny(_persistence.RestoreCompleted, Task.Delay(3000));

        var identity = _store.Auth.IsAuthenticated
            ? new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, _store.Auth.UserId!.ToString()!),
                new Claim(ClaimTypes.Name, _store.Auth.FullName ?? "المستخدم"),
            }, "EjarV2Auth")
            : new ClaimsIdentity();

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private void OnStoreChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public void Dispose() => _store.OnChanged -= OnStoreChanged;
}
