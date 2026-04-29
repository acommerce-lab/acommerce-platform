using System.Text.Json;
using Microsoft.JSInterop;

namespace Ejar.Customer.UI.Store;

/// <summary>
/// يحفظ ويستعيد حالة <see cref="AppStore"/> القابلة للديمومة (Auth + Favorites
/// + Recent Searches + Ui prefs) عبر localStorage. بدونه:
/// <list type="bullet">
///   <item>يُمسح JWT عند refresh → المستخدم يخرج تلقائياً.</item>
///   <item>تُنسى المفضّلات بعد إعادة التحميل.</item>
///   <item>يفقد التطبيق المسوّدة (DraftListing).</item>
/// </list>
///
/// <para>الاستخدام:
/// <list type="number">
///   <item>تُحقن خدمة الـ persistence في DI (scoped).</item>
///   <item>تُستدعى <see cref="RestoreAsync"/> مرّة عند بدء التطبيق (في
///         <c>MainLayout.OnAfterRenderAsync(firstRender: true)</c>).</item>
///   <item>تشترك في <c>store.OnChanged</c> لتحفظ تلقائياً عند كلّ تغيير.</item>
/// </list></para>
///
/// <para>WASM: يستخدم <c>localStorage</c> مباشرةً (يعمل قبل تحميل Blazor كاملاً).
/// Blazor Server: يستخدم <c>IJSRuntime</c> بعد أوّل render — لذلك المسؤوليّة
/// على <c>MainLayout</c> أن يستدعي <see cref="RestoreAsync"/> في firstRender.</para>
/// </summary>
public sealed class AppStorePersistence : IAsyncDisposable
{
    private const string KeyAuth        = "ejar.auth";
    private const string KeyFavorites   = "ejar.favorites";
    private const string KeyRecent      = "ejar.recent";
    private const string KeyUi          = "ejar.ui";

    private readonly AppStore _store;
    private readonly IJSRuntime _js;
    private bool _restored;
    private bool _suspendSave;
    // eager init — لو اكتملت RestoreAsync قبل أن يطلب أحد RestoreCompleted،
    // نتيجة TrySetResult تُحفَظ ولا تُفقَد. lazy init كان يُسقطها.
    private readonly TaskCompletionSource<bool> _restoreTcs = new();

    public AppStorePersistence(AppStore store, IJSRuntime js)
    {
        _store = store;
        _js    = js;
        _store.OnChanged += OnStoreChanged;
    }

    /// <summary>
    /// مهمّة تكتمل عندما يتمّ استعادة الحالة (نجاحاً أو إخفاقاً) لأوّل مرّة.
    /// تستخدمها <c>EjarAuthenticationStateProvider</c> لتأخير قرار
    /// <c>IsAuthenticated</c> حتى تُستعاد القيم من localStorage — وإلّا
    /// الصفحات المحميّة تُقيَّم قبل الاستعادة وتُعيد التوجيه إلى /login حتى
    /// لو كان الـ JWT محفوظاً.
    /// </summary>
    public Task RestoreCompleted => _restoreTcs.Task;

    /// <summary>
    /// يقرأ كلّ المفاتيح من localStorage ويُسقطها على <see cref="AppStore"/>.
    /// آمن للاستدعاء أكثر من مرّة — لا يفعل شيئاً بعد أوّل نجاح.
    /// </summary>
    public async Task RestoreAsync()
    {
        if (_restored) return;
        _suspendSave = true;
        try
        {
            await TryRestoreAuth();
            await TryRestoreFavorites();
            await TryRestoreRecent();
            await TryRestoreUi();
        }
        catch
        {
            // فشل JSInterop (مثلاً قبل اكتمال render في Blazor Server) — نتجاهل،
            // المُستدعي يستطيع المحاولة مرّة أخرى. لا نُعلِم RestoreCompleted
            // حتى يتمكّن AuthenticationStateProvider من إعادة المحاولة لاحقاً.
            _suspendSave = false;
            return;
        }

        _suspendSave = false;
        _restored = true;
        _restoreTcs.TrySetResult(true);
        _store.NotifyChanged();
    }

    public async ValueTask DisposeAsync()
    {
        _store.OnChanged -= OnStoreChanged;
        await ValueTask.CompletedTask;
    }

    private async void OnStoreChanged()
    {
        if (_suspendSave || !_restored) return;
        try
        {
            await SetItem(KeyAuth, new AuthSnapshot(
                _store.Auth.UserId, _store.Auth.FullName,
                _store.Auth.Phone,  _store.Auth.AccessToken));
            await SetItem(KeyFavorites, _store.FavoriteListingIds.ToArray());
            await SetItem(KeyRecent,    _store.RecentSearches.ToArray());
            await SetItem(KeyUi, new UiSnapshot(
                _store.Ui.Theme, _store.Ui.Culture.Language, _store.Ui.City));
        }
        catch
        {
            // فشل JSInterop غير قاتل — التغيير في الذاكرة يبقى ساري المفعول.
        }
    }

    private async Task TryRestoreAuth()
    {
        var s = await GetItem<AuthSnapshot>(KeyAuth);
        if (s is null) return;
        _store.Auth.UserId      = s.UserId;
        _store.Auth.FullName    = s.FullName;
        _store.Auth.Phone       = s.Phone;
        _store.Auth.AccessToken = s.AccessToken;
    }

    private async Task TryRestoreFavorites()
    {
        var ids = await GetItem<string[]>(KeyFavorites);
        if (ids is null) return;
        _store.FavoriteListingIds.Clear();
        foreach (var id in ids) _store.FavoriteListingIds.Add(id);
    }

    private async Task TryRestoreRecent()
    {
        var arr = await GetItem<string[]>(KeyRecent);
        if (arr is null) return;
        _store.RecentSearches.Clear();
        _store.RecentSearches.AddRange(arr);
    }

    private async Task TryRestoreUi()
    {
        var s = await GetItem<UiSnapshot>(KeyUi);
        if (s is null) return;
        _store.Ui.Theme      = s.Theme ?? _store.Ui.Theme;
        if (s.City is not null) _store.Ui.City = s.City;
        if (s.Language is not null && s.Language != _store.Ui.Culture.Language)
            _store.Ui.Culture = _store.Ui.Culture with { Language = s.Language };
    }

    private async Task<T?> GetItem<T>(string key)
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", key);
            if (string.IsNullOrEmpty(raw)) return default;
            return JsonSerializer.Deserialize<T>(raw);
        }
        catch { return default; }
    }

    private Task SetItem<T>(string key, T value) =>
        _js.InvokeVoidAsync("localStorage.setItem", key, JsonSerializer.Serialize(value)).AsTask();

    private sealed record AuthSnapshot(Guid? UserId, string? FullName, string? Phone, string? AccessToken);
    private sealed record UiSnapshot(string Theme, string Language, string City);
}
