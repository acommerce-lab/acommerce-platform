namespace ACommerce.Kits.Favorites.Frontend.Customer.Stores;

/// <summary>
/// تَنفيذ افتراضيّ لـ <see cref="IFavoritesStore"/> يَدلّع لـ
/// <see cref="IFavoritesApiClient"/>. التَطبيقات التي تَحتاج
/// optimistic toggle أو sync بين أجهزة تَكتب Binding خاصّ.
/// </summary>
public sealed class DefaultFavoritesStore : IFavoritesStore
{
    private readonly IFavoritesApiClient _api;
    private HashSet<string> _ids = new();

    public DefaultFavoritesStore(IFavoritesApiClient api) => _api = api;

    public IReadOnlyCollection<string> Ids => _ids;
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            _ids = (await _api.ListAsync(ct)).ToHashSet();
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task ToggleAsync(string targetId, CancellationToken ct = default)
    {
        var res = await _api.ToggleListingAsync(targetId, ct);
        if (!res.Success) return;
        if (res.IsFavorited) _ids.Add(targetId);
        else                  _ids.Remove(targetId);
        Changed?.Invoke();
    }

    public bool IsFavorited(string targetId) => _ids.Contains(targetId);
}
