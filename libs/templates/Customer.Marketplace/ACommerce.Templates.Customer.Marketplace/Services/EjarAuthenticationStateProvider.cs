using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Services;

/// <summary>
/// مزوّد حالة المصادقة. ينتظر <see cref="AppStorePersistence"/> ينتهي من استعادة
/// JWT من localStorage قبل أن يُجيب على <see cref="GetAuthenticationStateAsync"/>،
/// وإلّا الصفحات المحميّة تُقيَّم قبل الاستعادة وتعيد المستخدم إلى /login حتى لو
/// كان مسجَّل دخوله في الجلسة السابقة.
/// </summary>
public class EjarAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly AppStore _store;
    private readonly AppStorePersistence _persistence;

    public EjarAuthenticationStateProvider(AppStore store, AppStorePersistence persistence)
    {
        _store = store;
        _persistence = persistence;
        _store.OnChanged += OnStoreChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // كلّ استدعاء يُحاول الاستعادة ما لم تَنجح بَعد — Blazor Server يُنفِّذ
        // SSR أولاً (JSInterop غير مُتَوَفِّر، RestoreAsync يَفشل صامتاً ولا
        // يَضع TCS) ثمّ يُعاد الاستدعاء بَعد interactive bind فيَنجح. الـ flag
        // _kickedOff السابق كان يَمنع المحاولة الثانية ⇒ بَعد reload يَبقى
        // "غير مُصادَق" حتى لو JWT مَحفوظ في localStorage.
        if (!_persistence.RestoreCompleted.IsCompleted)
            _ = _persistence.RestoreAsync();

        await Task.WhenAny(_persistence.RestoreCompleted, Task.Delay(3000));

        var identity = _store.Auth.IsAuthenticated
            ? new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, _store.Auth.UserId!.ToString()!),
                new Claim(ClaimTypes.Name, _store.Auth.FullName ?? "المستخدم")
            }, "EjarAuth")
            : new ClaimsIdentity();

        return new AuthenticationState(new ClaimsPrincipal(identity));
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
