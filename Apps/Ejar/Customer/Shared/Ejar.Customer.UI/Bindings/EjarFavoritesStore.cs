using ACommerce.Kits.Favorites.Frontend.Customer.Stores;
using Ejar.Customer.UI.Services;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Bindings;

/// <summary>
/// تنفيذ <see cref="IFavoritesStore"/> فوق <see cref="FavoritesSync"/>
/// و<see cref="AppStore"/>. الـ Sync يُدير الجانب الشبكيّ + optimistic update،
/// والـ store يَكشف الواجهة المحايدة kit-aware. AppStore.OnChanged يُطلق
/// Changed تلقائياً ⇒ كلّ الصفحات تُعيد render عند تغيير المفضّلات.
/// </summary>
public sealed class EjarFavoritesStore : IFavoritesStore, IDisposable
{
    private readonly AppStore _app;
    private readonly FavoritesSync _sync;

    public EjarFavoritesStore(AppStore app, FavoritesSync sync)
    {
        _app = app;
        _sync = sync;
        _app.OnChanged += FireChanged;
        _sync.Changed  += FireChanged;
    }

    public IReadOnlyCollection<string> Ids => _app.FavoriteListingIds;
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; FireChanged();
        try   { await _sync.LoadFromServerAsync(ct); }
        finally { IsLoading = false; FireChanged(); }
    }

    public Task ToggleAsync(string targetId, CancellationToken ct = default) =>
        _sync.ToggleAsync(targetId, ct);

    public bool IsFavorited(string targetId) => _app.FavoriteListingIds.Contains(targetId);

    private void FireChanged() => Changed?.Invoke();

    public void Dispose()
    {
        _app.OnChanged -= FireChanged;
        _sync.Changed  -= FireChanged;
    }
}
