using ACommerce.Kits.Favorites.Frontend.Customer.Stores;

namespace Ejar.Customer.UI.V2.Bindings;

/// <summary>
/// تَنفيذ V2 لـ <see cref="IFavoritesStore"/>. يَدلّع لـ
/// <see cref="IFavoritesApiClient"/> الكيتيّ — لا FavoritesSync ولا
/// optimistic update في الـ app (سُلوك الـ kit يُغطّي هذا داخلياً).
/// </summary>
public sealed class EjarV2FavoritesStore : IFavoritesStore
{
    private readonly IFavoritesApiClient _api;
    private HashSet<string> _ids = new();

    public EjarV2FavoritesStore(IFavoritesApiClient api) => _api = api;

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
