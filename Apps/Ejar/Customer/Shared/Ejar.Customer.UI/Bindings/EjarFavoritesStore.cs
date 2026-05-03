using ACommerce.Kits.Favorites.Frontend.Customer.Stores;

namespace Ejar.Customer.UI.Bindings;

public sealed class EjarFavoritesStore : IFavoritesStore
{
    private readonly HashSet<string> _ids = new();
    public IReadOnlyCollection<string> Ids => _ids;
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public Task LoadAsync(CancellationToken ct = default) { Changed?.Invoke(); return Task.CompletedTask; }

    public Task ToggleAsync(string targetId, CancellationToken ct = default)
    {
        if (!_ids.Add(targetId)) _ids.Remove(targetId);
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public bool IsFavorited(string targetId) => _ids.Contains(targetId);
}
