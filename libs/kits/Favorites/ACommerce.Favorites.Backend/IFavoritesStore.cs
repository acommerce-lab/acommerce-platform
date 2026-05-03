namespace ACommerce.Favorites.Backend;

/// <summary>
/// عقد المفضّلات — التطبيق ينفّذه. Favorites kit يَستهلكه:
/// <list type="bullet">
///   <item><c>ListMineAsync</c>: ل /favorites — قائمة المفضّلة للمستخدم.</item>
///   <item><c>ToggleAsync</c>: ل /listings/{id}/favorite — يبدّل الإضافة/الحذف.</item>
/// </list>
///
/// <para>عقد <see cref="FavoriteToggleResult"/> = الحالة بعد التبديل + متى
/// أضيف. التطبيق قد يَجمع join مع entities أخرى (Listings) لإثراء الـ
/// <c>ListMineAsync</c> بـ thumbnail/title — ذلك من اختصاصه.</para>
/// </summary>
public interface IFavoritesStore
{
    /// <summary>قائمة مفضّلات المستخدم. عناصر مع title/thumbnail/price لو
    /// التطبيق join مع Listings، أو فقط ids إن لا.</summary>
    Task<IReadOnlyList<object>> ListMineAsync(string userId, CancellationToken ct);

    /// <summary>يبدّل favorite — يضيف لو غير موجود، يحذف لو موجود. F6: tracker-only.</summary>
    Task<FavoriteToggleResult> ToggleNoSaveAsync(string userId, string entityType, string entityId, CancellationToken ct);
}

public sealed record FavoriteToggleResult(string Id, bool IsFavorite);
