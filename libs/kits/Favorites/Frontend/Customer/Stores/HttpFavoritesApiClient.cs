using ACommerce.ClientHost.KitApi;

namespace ACommerce.Kits.Favorites.Frontend.Customer.Stores;

public sealed class HttpFavoritesApiClient : IFavoritesApiClient
{
    private const string Kit = "favorites";
    private readonly KitHttpClient _http;

    public HttpFavoritesApiClient(KitHttpClient http) => _http = http;

    public async Task<IReadOnlyCollection<string>> ListAsync(CancellationToken ct = default)
    {
        var res = await _http.GetAsync<List<FavoriteRow>>(Kit, "/favorites", ct);
        if (!res.Success || res.Data is null) return Array.Empty<string>();
        return res.Data.Where(r => !string.IsNullOrEmpty(r.Id)).Select(r => r.Id).ToList();
    }

    public async Task<FavoriteToggleResult> ToggleListingAsync(string listingId, CancellationToken ct = default)
    {
        var res = await _http.PostAsync<ToggleDto>(Kit,
            $"/listings/{Uri.EscapeDataString(listingId)}/favorite", null, ct);
        if (!res.Success || res.Data is null)
            return new FavoriteToggleResult(false, false);
        return new FavoriteToggleResult(true, res.Data.IsFavorite);
    }

    private sealed record FavoriteRow(string Id);
    private sealed record ToggleDto(string Id, bool IsFavorite);
}
