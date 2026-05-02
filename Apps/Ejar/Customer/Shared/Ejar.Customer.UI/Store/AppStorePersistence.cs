using System.Text.Json;
using Microsoft.JSInterop;

namespace Ejar.Customer.UI.Store;

/// <summary>
/// يحفظ ويستعيد حالة <see cref="AppStore"/> القابلة للديمومة عبر localStorage.
///
/// <para>ما يُحفَظ محلّياً ولماذا:
/// <list type="bullet">
///   <item><b>Auth (JWT + UserId + الاسم + الهاتف)</b> — حتّى لا يُطلَب من المستخدم
///         إعادة تسجيل الدخول مع كلّ refresh. تنتهي صلاحيّته من الخادم.</item>
///   <item><b>RecentSearches</b> — مساعدة UX خفيفة (سلسلة نصوص قصيرة).</item>
///   <item><b>Ui prefs (Theme + Language + City)</b> — تجربة مستخدم متّسقة بين الجلسات.</item>
/// </list></para>
///
/// <para>ما لا يُحفَظ محلّياً عمداً:
/// <list type="bullet">
///   <item><b>المفضّلات</b> — الخادم هو الحقيقة الوحيدة (مزامنة عبر الأجهزة).
///         <see cref="FavoritesSync.LoadFromServerAsync"/> يُحضرها بعد الاستعادة.
///         تخزينها محلّياً كان يُنتج بقايا تظلّ بعد تسجيل الخروج، أو نسخاً
///         قديمة على جهاز ينافس بيانات الخادم.</item>
///   <item><b>محتوى الإعلانات/الفئات/المدن</b> — تُجلَب عند الحاجة (online-first).
///         إدارة cache invalidation للمحتوى الديناميّ يستدعي بنية أكبر بكثير
///         (ETags، إبطال خادميّ…) لا نملكها الآن، فالأبسط أن نعتمد على الاتصال
///         دائماً ونستخدم HTTP cache من المتصفّح فقط.</item>
/// </list></para>
///
/// <para><b>إبطال الكاش</b>: عند تسجيل الخروج <c>AuthInterpreter</c> أو
/// <c>Me.razor.SignOut</c> يمسحان كلّ شيء (Favorites/Recent/Draft/Auth) من
/// AppStore، فيلتقط <c>OnStoreChanged</c> القيم الجديدة (الفارغة) ويكتبها
/// إلى localStorage فوراً.</para>
/// </summary>
public sealed class AppStorePersistence : IAsyncDisposable
{
    private const string KeyAuth        = "ejar.auth";
    // KeyFavorites أُسقط — الخادم هو الحقيقة الوحيدة. كان مفتاح "ejar.favorites"
    // يُنشئ بقايا (heart مضاء بعد signout، مزامنة فاشلة بين الأجهزة).
    private const string KeyFavoritesLegacy = "ejar.favorites";
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
            await PurgeLegacyFavoritesAsync();
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
            // المفضّلات لم تعد تُحفَظ — الخادم هو المصدر، يُحضرها FavoritesSync
            // بعد كلّ تسجيل دخول و كلّ بدء جلسة.
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

    /// <summary>
    /// يمسح مفتاح <c>ejar.favorites</c> القديم من localStorage إن وُجد.
    /// المستخدمون الذين يحدّثون من نسخة قديمة قد يكون لديهم بقايا تظلّ بعد
    /// signout. لا نستعيدها — FavoritesSync يجلبها من الخادم.
    /// </summary>
    private async Task PurgeLegacyFavoritesAsync()
    {
        try { await _js.InvokeVoidAsync("localStorage.removeItem", KeyFavoritesLegacy); }
        catch { /* JSInterop غير متاح — ضرر يسيطر */ }
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
        // ترحيل لمرّة واحدة: المستخدمون القدامى الذين خزّن لهم localStorage
        // "صنعاء" بوصفها المدينة الافتراضيّة (قبل تحوّل التطبيق لسوق إب)
        // يُنقَلون لـ "إب" تلقائياً. لو كان قد غيّرها المستخدم يدويّاً لمدينة
        // أخرى تُحترَم. للعودة لصنعاء (لو رغب) → AcCityPicker على الـ home.
        if (s.City is not null && s.City != "صنعاء") _store.Ui.City = s.City;
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
