using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ACommerce.ClientHost.Auth;
using ACommerce.Kits.Favorites.Frontend.Customer.Stores;

// Namespace kept as Ejar.Customer.UI.Services for V1 page compat.
namespace Ejar.Customer.UI.Services;

/// <summary>
/// composition تُعيد تَزامُن المُفَضَّلات مَع الخادم + optimistic UI.
/// تُغَذّي <see cref="DefaultFavoritesStore.IngestRealtimeToggle"/> فيُحَدِّث
/// <c>IFavoritesStore.Ids</c> فَوراً قَبل ردّ الخادم.
///
/// <para>F64 Phase 2: انتُزِعَت مِن template Customer.Marketplace. تَعتَمِد
/// عَلى <see cref="IClientAuthState"/> + <see cref="AuthenticatedHttpClient"/>
/// بَدَل V1's AppStore + ApiReader. تَطبيقات تَستَخدِمها بِـ:
/// <code>services.AddCustomerFavoritesSync();</code></para>
/// </summary>
public sealed class FavoritesSync
{
    private readonly IClientAuthState _auth;
    private readonly AuthenticatedHttpClient _http;
    private readonly IFavoritesStore _store;

    public FavoritesSync(
        IClientAuthState auth,
        AuthenticatedHttpClient http,
        IFavoritesStore store)
    {
        _auth = auth;
        _http = http;
        _store = store;
    }

    public string? LastError { get; private set; }
    public event Action? Changed;

    public async Task LoadFromServerAsync(CancellationToken ct = default)
    {
        if (!_auth.IsAuthenticated) return;
        await _store.LoadAsync(ct);
    }

    public async Task<bool> ToggleAsync(string listingId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(listingId)) return false;
        SetError(null);

        // Optimistic flip لِواجِهة المُستَخدِم
        var defaultStore = _store as DefaultFavoritesStore;
        var wasFav = _store.IsFavorited(listingId);
        defaultStore?.IngestRealtimeToggle(listingId, !wasFav);

        if (!_auth.IsAuthenticated)
            return !wasFav; // Local-only toggle (lazy login flow)

        try
        {
            var resp = await _http.Client.PostAsJsonAsync(
                $"/listings/{Uri.EscapeDataString(listingId)}/favorite", new { }, ct);

            if (!resp.IsSuccessStatusCode)
            {
                // Revert optimistic flip
                defaultStore?.IngestRealtimeToggle(listingId, wasFav);
                SetError($"تعذّر حفظ المفضّلة (HTTP {(int)resp.StatusCode})");
                return wasFav;
            }

            var result = await resp.Content.ReadFromJsonAsync<ToggleResult>(ct);
            if (result is not null && result.IsFavorite != !wasFav)
            {
                // Server's truth differs from optimistic — apply it
                defaultStore?.IngestRealtimeToggle(listingId, result.IsFavorite);
            }
            return result?.IsFavorite ?? !wasFav;
        }
        catch (Exception ex)
        {
            defaultStore?.IngestRealtimeToggle(listingId, wasFav);
            SetError($"تعذّر الاتصال بالخادم: {ex.Message}");
            return wasFav;
        }
    }

    private void SetError(string? msg)
    {
        if (LastError == msg) return;
        LastError = msg;
        Changed?.Invoke();
    }

    private sealed record ToggleResult(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("isFavorite")] bool IsFavorite);
}
