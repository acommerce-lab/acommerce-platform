namespace ACommerce.Kits.Favorites.Frontend.Customer.Stores;

/// <summary>
/// store reactive للمفضّلات. يَحوي ids فقط — العرض الكامل (صورة، عنوان…)
/// يَأتي عبر joins داخل التطبيق أو طلبات Listings/Products منفصلة. هذا
/// يُبقي الـ Favorites kit حياديّاً عن نوع المُفضَّل.
/// </summary>
public interface IFavoritesStore
{
    /// <summary>ids العناصر المفضّلة الحاليّة.</summary>
    IReadOnlyCollection<string> Ids { get; }
    bool IsLoading { get; }
    event Action? Changed;

    Task LoadAsync(CancellationToken ct = default);
    Task ToggleAsync(string targetId, CancellationToken ct = default);
    bool IsFavorited(string targetId);
}
