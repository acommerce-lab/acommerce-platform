using System.Text.Json;
using Microsoft.JSInterop;

namespace ACommerce.ClientHost.Auth;

/// <summary>
/// يَحفظ ويَستعيد <see cref="IClientAuthState"/> في localStorage. JSInterop
/// قد يَفشل في SSR (Blazor Server يُؤخّر تَوَفّره) — نَلتقط الفَشل صامتاً
/// ونَترك TCS غير مُكمَل ليَعيد المُتّصِل المُحاولة بَعد interactive bind.
///
/// <para>الـ key يُحقَن من التَطبيق عبر <see cref="ClientAuthPersistenceOptions"/>
/// — هذه هي النُّقطة الوَحيدة التي يَتَغَيَّر فيها سُلوك الـ Persistence
/// بين تَطبيقات.</para>
/// </summary>
public sealed class LocalStorageClientAuthPersistence : IClientAuthPersistence
{
    private readonly IClientAuthState _state;
    private readonly IJSRuntime _js;
    private readonly string _key;
    private readonly TaskCompletionSource<bool> _tcs = new();
    private bool _restored;
    private bool _suspendSave;

    public LocalStorageClientAuthPersistence(
        IClientAuthState state,
        IJSRuntime js,
        ClientAuthPersistenceOptions options)
    {
        _state = state;
        _js = js;
        _key = options.StorageKey;
        _state.OnChanged += OnStateChanged;
    }

    public Task RestoreCompleted => _tcs.Task;

    public async Task RestoreAsync()
    {
        if (_restored) return;
        _suspendSave = true;
        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", _key);
            if (!string.IsNullOrEmpty(raw))
            {
                var s = JsonSerializer.Deserialize<AuthSnapshot>(raw);
                if (s is not null)
                {
                    _state.UserId      = s.UserId;
                    _state.FullName    = s.FullName;
                    _state.Phone       = s.Phone;
                    _state.AccessToken = s.AccessToken;
                    _state.Role        = s.Role;
                }
            }
            _suspendSave = false;
            _restored = true;
            _tcs.TrySetResult(true);
            _state.NotifyChanged();
        }
        catch
        {
            _suspendSave = false;
            // JS interop غير مُتَوَفِّر بَعد (SSR/prerender) — لا نُكمِل TCS
        }
    }

    private async void OnStateChanged()
    {
        if (_suspendSave || !_restored) return;
        try
        {
            var json = JsonSerializer.Serialize(new AuthSnapshot(
                _state.UserId, _state.FullName, _state.Phone,
                _state.AccessToken, _state.Role));
            await _js.InvokeVoidAsync("localStorage.setItem", _key, json);
        }
        catch { }
    }

    private sealed record AuthSnapshot(
        Guid? UserId, string? FullName, string? Phone,
        string? AccessToken, string? Role);
}

/// <summary>إعدادات تَخزين الـ Auth — key محلّيّ يَكشف هويّة التَطبيق.</summary>
public sealed record ClientAuthPersistenceOptions(string StorageKey);
