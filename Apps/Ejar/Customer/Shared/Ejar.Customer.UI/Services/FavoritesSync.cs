using ACommerce.Kits.Favorites.Frontend.Customer.Stores;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Services;

/// <summary>
/// يُزامن المفضّلات بين <see cref="AppStore.FavoriteListingIds"/> (محلّياً في
/// localStorage) والخادم عبر <see cref="IFavoritesApiClient"/>:
/// <list type="bullet">
///   <item>نضغط القلب → نُحدّث المحلّيّ تفاؤليّاً + نستدعي الخادم. لو فشل
///         الخادم نُرجع الحالة المحلّيّة + نُعلن الخطأ.</item>
///   <item>عند بدء الجلسة (بعد <c>RestoreAsync</c> أو بعد تسجيل الدخول)
///         نُحمّل القائمة من الخادم لتطغى على ما في localStorage — حتّى
///         تظهر المفضّلات على كلّ الأجهزة.</item>
/// </list>
/// </summary>
public sealed class FavoritesSync
{
    private readonly AppStore _store;
    private readonly IFavoritesApiClient _api;

    public FavoritesSync(AppStore store, IFavoritesApiClient api)
    {
        _store = store;
        _api   = api;
    }

    /// <summary>
    /// آخر خطأ شبكيّ/خادم عند آخر نداء toggle. تُمسح في كلّ نداء جديد ناجح.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>يُطلَق عند تغيّر <see cref="LastError"/>.</summary>
    public event Action? Changed;

    public async Task LoadFromServerAsync(CancellationToken ct = default)
    {
        if (!_store.Auth.IsAuthenticated) return;
        var ids = await _api.ListAsync(ct);
        _store.FavoriteListingIds.Clear();
        foreach (var id in ids)
            if (!string.IsNullOrEmpty(id))
                _store.FavoriteListingIds.Add(id);
        _store.NotifyChanged();
    }

    public async Task<bool> ToggleAsync(string listingId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(listingId)) return false;
        if (!_store.Auth.IsAuthenticated)
        {
            // غير مصادَق — تَحديث محلّيّ فقط ريثما يَدخل المستخدم.
            var added = _store.FavoriteListingIds.Add(listingId);
            if (!added) _store.FavoriteListingIds.Remove(listingId);
            _store.NotifyChanged();
            return added;
        }

        // تَحديث تفاؤليّ — يَقفز القلب فوراً.
        bool optimisticOn = _store.FavoriteListingIds.Add(listingId);
        if (!optimisticOn) _store.FavoriteListingIds.Remove(listingId);
        _store.NotifyChanged();

        SetError(null);

        var res = await _api.ToggleListingAsync(listingId, ct);
        if (!res.Success)
        {
            // فَشِل — أرجع الحالة وأعلن الخطأ.
            if (optimisticOn) _store.FavoriteListingIds.Remove(listingId);
            else              _store.FavoriteListingIds.Add(listingId);
            _store.NotifyChanged();
            SetError("تعذّر حفظ المفضّلة على الخادم — حاول مجدّداً.");
            return !optimisticOn;
        }

        // الخادم هو الحقيقة — اضبط المحلّيّ على ما أرجعه (في حال انحراف).
        if (res.IsFavorited)
            _store.FavoriteListingIds.Add(listingId);
        else
            _store.FavoriteListingIds.Remove(listingId);
        _store.NotifyChanged();
        return res.IsFavorited;
    }

    private void SetError(string? msg)
    {
        if (LastError == msg) return;
        LastError = msg;
        Changed?.Invoke();
    }
}
