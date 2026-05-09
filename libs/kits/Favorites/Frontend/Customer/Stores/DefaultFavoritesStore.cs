using ACommerce.Client.Operations;

namespace ACommerce.Kits.Favorites.Frontend.Customer.Stores;

/// <summary>
/// OAM-shaped (F61). favorite.toggle مَوسوم بـ realtime_broadcast فتَحقن
/// composition Realtime مُعتَرضاً يُعلِم الأَجهزة الأُخرى لِنَفس المُستَخدِم.
/// </summary>
public sealed class DefaultFavoritesStore : IFavoritesStore
{
    private readonly ITemplateEngine _engine;
    private HashSet<string> _ids = new();

    public DefaultFavoritesStore(ITemplateEngine engine) => _engine = engine;

    public IReadOnlyCollection<string> Ids => _ids;
    public bool IsLoading { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true; Changed?.Invoke();
        try
        {
            var env = await _engine.ExecuteAsync<List<string>>(FavoritesOps.List(), ct: ct);
            if (env.Operation.Status == "Success" && env.Data is not null)
                _ids = env.Data.ToHashSet();
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task ToggleAsync(string targetId, CancellationToken ct = default)
    {
        var env = await _engine.ExecuteAsync<ToggleResultDto>(FavoritesOps.Toggle(targetId), ct: ct);
        if (env.Operation.Status != "Success" || env.Data is null) return;
        if (env.Data.IsFavorited) _ids.Add(targetId);
        else                       _ids.Remove(targetId);
        Changed?.Invoke();
    }

    public bool IsFavorited(string targetId) => _ids.Contains(targetId);

    /// <summary>مَدخَل realtime: composition تَدفَع تَغيُّراً مِن جِهاز آخَر.</summary>
    public void IngestRealtimeToggle(string listingId, bool isFavorited)
    {
        if (isFavorited) _ids.Add(listingId);
        else              _ids.Remove(listingId);
        Changed?.Invoke();
    }

    private sealed record ToggleResultDto(bool IsFavorited, int Count);
}
