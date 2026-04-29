using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Services;

/// <summary>
/// يُزامن المفضّلات بين <see cref="AppStore.FavoriteListingIds"/> (محلّياً في
/// localStorage) والخادم (<c>POST /listings/{id}/favorite</c> +
/// <c>GET /favorites</c>). قبل هذه الخدمة كانت المفضّلات تُكتب على
/// <c>localStorage</c> فقط — عند فتح التطبيق من جهاز آخر لم تظهر، وعند تنظيف
/// كاش المتصفّح كانت تُمحى. الآن:
/// <list type="bullet">
///   <item>نضغط القلب → نُحدّث المحلّيّ تفاؤليّاً + نستدعي الخادم. لو فشل
///         الخادم نُرجع الحالة المحلّيّة.</item>
///   <item>عند بدء الجلسة (بعد <c>RestoreAsync</c> أو بعد تسجيل الدخول)
///         نُحمّل القائمة من الخادم لتطغى على ما في localStorage — حتّى
///         تظهر المفضّلات على كلّ الأجهزة.</item>
/// </list>
/// </summary>
public sealed class FavoritesSync
{
    private readonly AppStore _store;
    private readonly ApiReader _api;

    public FavoritesSync(AppStore store, ApiReader api)
    {
        _store = store;
        _api   = api;
    }

    /// <summary>
    /// يُحمّل قائمة المفضّلات من الخادم ويستبدل بها <see cref="AppStore.FavoriteListingIds"/>.
    /// يُستدعى مرّة بعد استعادة الجلسة وبعد تسجيل الدخول. لا يفعل شيئاً لو
    /// المستخدم غير مصادَق.
    /// </summary>
    public async Task LoadFromServerAsync(CancellationToken ct = default)
    {
        if (!_store.Auth.IsAuthenticated) return;
        var env = await _api.GetAsync<List<FavoriteRow>>("/favorites", ct: ct);
        if (env.Operation.Status != "Success" || env.Data is null) return;

        _store.FavoriteListingIds.Clear();
        foreach (var row in env.Data)
        {
            if (!string.IsNullOrEmpty(row.Id))
                _store.FavoriteListingIds.Add(row.Id);
        }
        _store.NotifyChanged();
    }

    /// <summary>
    /// يُبدّل حالة المفضّلة لإعلان: تحديث محلّيّ تفاؤليّ ثمّ استدعاء
    /// <c>POST /listings/{id}/favorite</c>. يُرجع الحالة الجديدة (true لو
    /// أصبحت مفضّلة). لو فشل الخادم نُعيد الحالة المحلّيّة لتعكس الحقيقة.
    /// </summary>
    public async Task<bool> ToggleAsync(string listingId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(listingId)) return false;
        if (!_store.Auth.IsAuthenticated)
        {
            // غير مصادَق — نُحدّث المحلّيّ فقط (يحفظه persistence) ريثما يسجّل
            // المستخدم الدخول، ثمّ سيُدمج لاحقاً عبر LoadFromServerAsync.
            var added = _store.FavoriteListingIds.Add(listingId);
            if (!added) _store.FavoriteListingIds.Remove(listingId);
            _store.NotifyChanged();
            return added;
        }

        // تحديث تفاؤليّ — يقفز القلب فوراً، حتّى لو الخادم بطيء.
        bool optimisticOn = _store.FavoriteListingIds.Add(listingId);
        if (!optimisticOn) _store.FavoriteListingIds.Remove(listingId);
        _store.NotifyChanged();

        var env = await _api.PostAsync<FavoriteToggleResult>(
            $"/listings/{Uri.EscapeDataString(listingId)}/favorite", body: null, ct: ct);

        if (env.Operation.Status != "Success" || env.Data is null)
        {
            // فشل الخادم — تراجع: أعِد الحالة كما كانت قبل النقر.
            if (optimisticOn) _store.FavoriteListingIds.Remove(listingId);
            else              _store.FavoriteListingIds.Add(listingId);
            _store.NotifyChanged();
            return !optimisticOn;
        }

        // الخادم هو الحقيقة — اضبط المحلّيّ على ما أرجعه (في حال انحراف).
        if (env.Data.IsFavorite)
            _store.FavoriteListingIds.Add(listingId);
        else
            _store.FavoriteListingIds.Remove(listingId);
        _store.NotifyChanged();
        return env.Data.IsFavorite;
    }

    private sealed record FavoriteRow(string Id);
    private sealed record FavoriteToggleResult(string Id, bool IsFavorite);
}
