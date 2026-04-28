using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Services;

public class EjarAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly AppStore _store;

    public EjarAuthenticationStateProvider(AppStore store)
    {
        _store = store;
        _store.OnChanged += OnStoreChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = _store.Auth.IsAuthenticated
            ? new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, _store.Auth.UserId!.ToString()!),
                new Claim(ClaimTypes.Name, _store.Auth.FullName ?? "المستخدم")
            }, "EjarAuth")
            : new ClaimsIdentity();

        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    private void OnStoreChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void Dispose()
    {
        _store.OnChanged -= OnStoreChanged;
    }
}
