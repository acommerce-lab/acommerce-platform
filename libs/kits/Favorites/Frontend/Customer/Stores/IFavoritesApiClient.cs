namespace ACommerce.Kits.Favorites.Frontend.Customer.Stores;

/// <summary>
/// عميل HTTP خاصّ بـ Favorites kit. يَعرف <c>FavoritesController</c>:
/// <list type="bullet">
///   <item><c>GET /favorites</c> ⇒ <c>FavoriteRow[]</c> — يُستخرَج التَّعريف فقط.</item>
///   <item><c>POST /listings/{id}/favorite</c> ⇒ <c>{ isFavorited, count }</c></item>
/// </list>
/// </summary>
public interface IFavoritesApiClient
{
    /// <summary>قائمة ids للعناصر المُفَضَّلة (مَسطَّحة).</summary>
    Task<IReadOnlyCollection<string>> ListAsync(CancellationToken ct = default);

    /// <summary>POST toggle. يَردّ الحالة الجديدة (مَفضَّل أم لا).</summary>
    Task<FavoriteToggleResult> ToggleListingAsync(string listingId, CancellationToken ct = default);
}

public readonly record struct FavoriteToggleResult(bool Success, bool IsFavorited);
