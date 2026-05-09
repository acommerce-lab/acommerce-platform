using ACommerce.Kits.Favorites.Frontend.Customer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Compositions.Customer.Favorites.Realtime;

/// <summary>
/// مَدخَل realtime لِـ favorites: hub التَطبيق يَستَدعي <see cref="OnToggle"/>
/// عِند وُصول حَدَث toggle مِن جِهاز آخَر لِنَفس المُستَخدِم.
/// تُدفَع إلى <see cref="DefaultFavoritesStore.IngestRealtimeToggle"/>.
/// </summary>
public sealed class FavoritesRealtimeIngestor
{
    private readonly DefaultFavoritesStore _store;
    public FavoritesRealtimeIngestor(IFavoritesStore store) => _store = (DefaultFavoritesStore)store;

    public void OnToggle(string listingId, bool isFavorited) =>
        _store.IngestRealtimeToggle(listingId, isFavorited);
}

public static class FavoritesRealtimeExtensions
{
    public static IServiceCollection AddFavoritesRealtimeComposition(this IServiceCollection services)
    {
        services.AddScoped<FavoritesRealtimeIngestor>();
        return services;
    }
}
