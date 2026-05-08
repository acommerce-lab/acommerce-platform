using System.Text.Json;
using Microsoft.JSInterop;

namespace ACommerce.ClientHost.Preferences;

/// <summary>
/// يَحفظ ويَستعيد <see cref="IUiPreferences"/> في localStorage. JSInterop
/// قد يَفشل في SSR — لا يُكَمِّل TCS فيُعيد المُتَّصِل المُحاولة بَعد
/// interactive bind.
/// </summary>
public sealed class LocalStorageUiPersistence
{
    private readonly IUiPreferences _prefs;
    private readonly IJSRuntime _js;
    private readonly string _key;
    private readonly TaskCompletionSource<bool> _tcs = new();
    private bool _restored;
    private bool _suspendSave;

    public LocalStorageUiPersistence(IUiPreferences prefs, IJSRuntime js, UiPreferencesOptions options)
    {
        _prefs = prefs;
        _js = js;
        _key = options.StorageKey;
        _prefs.OnChanged += OnChanged;
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
                var s = JsonSerializer.Deserialize<UiSnapshot>(raw);
                if (s is not null)
                {
                    _prefs.Theme = s.Theme ?? _prefs.Theme;
                    if (!string.IsNullOrEmpty(s.City)) _prefs.City = s.City;
                    _prefs.RecentSearches.Clear();
                    if (s.RecentSearches is not null)
                        foreach (var r in s.RecentSearches) _prefs.RecentSearches.Add(r);
                }
            }
            _suspendSave = false;
            _restored = true;
            _tcs.TrySetResult(true);
            _prefs.NotifyChanged();
        }
        catch
        {
            _suspendSave = false;
        }
    }

    private async void OnChanged()
    {
        if (_suspendSave || !_restored) return;
        try
        {
            var json = JsonSerializer.Serialize(new UiSnapshot(
                _prefs.Theme, _prefs.City,
                _prefs.RecentSearches.ToArray()));
            await _js.InvokeVoidAsync("localStorage.setItem", _key, json);
        }
        catch { }
    }

    private sealed record UiSnapshot(string? Theme, string? City, string[]? RecentSearches);
}

public sealed record UiPreferencesOptions(string StorageKey);
