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
    private bool _kickedOff;

    public EjarAuthenticationStateProvider(AppStore store, AppStorePersistence persistence)
    {
        _store = store;
        _persistence = persistence;
        _store.OnChanged += OnStoreChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // أوّل استدعاء يبدأ الاستعادة (في WASM ينجح فوراً، في Blazor Server
        // قد يفشل JSInterop أثناء prerender — وفي تلك الحالة نُجيب بـ "غير
        // مصادَق عليه" مؤقتاً ثمّ نُحدِّث عبر MainLayout.OnAfterRenderAsync).
        if (!_kickedOff)
        {
            _kickedOff = true;
            _ = _persistence.RestoreAsync();
        }

        // ننتظر اكتمال الاستعادة بحدّ أقصى ثانيتَين — يكفي لـ WASM (فوريّ)
        // ولا نُعلِق UI لو فشل JSInterop في Blazor Server (يكمل لاحقاً عبر
        // OnAfterRenderAsync و NotifyAuthenticationStateChanged).
        await Task.WhenAny(_persistence.RestoreCompleted, Task.Delay(2000));

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
