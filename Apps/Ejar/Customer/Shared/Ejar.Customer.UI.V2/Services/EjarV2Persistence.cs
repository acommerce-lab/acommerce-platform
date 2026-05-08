using System.Text.Json;
using Microsoft.JSInterop;

namespace Ejar.Customer.UI.V2.Services;

/// <summary>
/// يَحفظ ويَستعيد <see cref="EjarV2AppStore.Auth"/> في localStorage. JSInterop
/// قد يَفشل في SSR (Blazor Server يُؤخّر تَوَفّره) — نَلتقط الفَشل صامتاً
/// ونَترك TCS غير مُكمَل ليَعيد المُتّصِل المُحاولة بَعد interactive bind.
/// </summary>
public sealed class EjarV2Persistence
{
    private const string KeyAuth = "ejar.v2.auth";
    private readonly EjarV2AppStore _store;
    private readonly IJSRuntime _js;
    private readonly TaskCompletionSource<bool> _tcs = new();
    private bool _restored;
    private bool _suspendSave;

    public EjarV2Persistence(EjarV2AppStore store, IJSRuntime js)
    {
        _store = store;
        _js = js;
        _store.OnChanged += OnStoreChanged;
    }

    public Task RestoreCompleted => _tcs.Task;

    public async Task RestoreAsync()
    {
        if (_restored) return;
        _suspendSave = true;
        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", KeyAuth);
            if (!string.IsNullOrEmpty(raw))
            {
                var s = JsonSerializer.Deserialize<AuthSnapshot>(raw);
                if (s is not null)
                {
                    _store.Auth.UserId      = s.UserId;
                    _store.Auth.FullName    = s.FullName;
                    _store.Auth.Phone       = s.Phone;
                    _store.Auth.AccessToken = s.AccessToken;
                }
            }
            _suspendSave = false;
            _restored = true;
            _tcs.TrySetResult(true);
            _store.NotifyChanged();
        }
        catch
        {
            _suspendSave = false;
            // JS interop غير مُتَوَفِّر بَعد (SSR/prerender) — لا نُكمِل TCS
        }
    }

    private async void OnStoreChanged()
    {
        if (_suspendSave || !_restored) return;
        try
        {
            var json = JsonSerializer.Serialize(new AuthSnapshot(
                _store.Auth.UserId, _store.Auth.FullName,
                _store.Auth.Phone,  _store.Auth.AccessToken));
            await _js.InvokeVoidAsync("localStorage.setItem", KeyAuth, json);
        }
        catch { }
    }

    private sealed record AuthSnapshot(Guid? UserId, string? FullName, string? Phone, string? AccessToken);
}
