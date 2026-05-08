using System.Text.Json.Serialization;
using ACommerce.OperationEngine.Wire;
using Ejar.Customer.UI.Store;

namespace Ejar.Customer.UI.Services;

/// <summary>
/// يُزامن المفضّلات بين <see cref="AppStore.FavoriteListingIds"/> والخادم
/// عبر <see cref="ApiReader"/> مباشرة (مَسار V1 المُجَرَّب). الـ
/// IFavoritesApiClient الجديد + KitHttpClient pipeline كانا يَفشلان في
/// مَسار toggle بدون body — رَجَعنا للنَهج البَسيط.
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

    public string? LastError { get; private set; }
    public event Action? Changed;

    public async Task LoadFromServerAsync(CancellationToken ct = default)
    {
        if (!_store.Auth.IsAuthenticated) return;
        var env = await _api.GetAsync<List<FavoriteRow>>("/favorites", ct: ct);
        if (env.Operation.Status != "Success" || env.Data is null) return;

        _store.FavoriteListingIds.Clear();
        foreach (var row in env.Data)
            if (!string.IsNullOrEmpty(row.Id))
                _store.FavoriteListingIds.Add(row.Id);
        _store.NotifyChanged();
    }

    public async Task<bool> ToggleAsync(string listingId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(listingId)) return false;
        if (!_store.Auth.IsAuthenticated)
        {
            var added = _store.FavoriteListingIds.Add(listingId);
            if (!added) _store.FavoriteListingIds.Remove(listingId);
            _store.NotifyChanged();
            return added;
        }

        bool optimisticOn = _store.FavoriteListingIds.Add(listingId);
        if (!optimisticOn) _store.FavoriteListingIds.Remove(listingId);
        _store.NotifyChanged();

        SetError(null);

        var env = await _api.PostAsync<ToggleResult>(
            $"/listings/{Uri.EscapeDataString(listingId)}/favorite", body: null, ct: ct);

        if (env.Operation.Status != "Success" || env.Data is null)
        {
            if (optimisticOn) _store.FavoriteListingIds.Remove(listingId);
            else              _store.FavoriteListingIds.Add(listingId);
            _store.NotifyChanged();

            var code = env.Error?.Code ?? env.Operation?.FailedAnalyzer;
            var msg  = env.Error?.Message ?? env.Operation?.ErrorMessage;
            SetError((code, msg) switch
            {
                ("network_error",     var m) when m is not null => $"تعذّر الاتصال بالخادم: {m}",
                ("listing_not_found", _)                        => "الإعلان لم يعد متوفّراً.",
                (_, var m) when !string.IsNullOrWhiteSpace(m)   => m!,
                (var c, _) when !string.IsNullOrWhiteSpace(c)   => $"تعذّر حفظ المفضّلة ({c}).",
                _                                                => "تعذّر حفظ المفضّلة على الخادم — حاول مجدّداً."
            });
            return !optimisticOn;
        }

        if (env.Data.IsFavorite)
            _store.FavoriteListingIds.Add(listingId);
        else
            _store.FavoriteListingIds.Remove(listingId);
        _store.NotifyChanged();
        return env.Data.IsFavorite;
    }

    private void SetError(string? msg)
    {
        if (LastError == msg) return;
        LastError = msg;
        Changed?.Invoke();
    }

    private sealed record FavoriteRow([property: JsonPropertyName("id")] string Id);
    private sealed record ToggleResult(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("isFavorite")] bool IsFavorite);
}
