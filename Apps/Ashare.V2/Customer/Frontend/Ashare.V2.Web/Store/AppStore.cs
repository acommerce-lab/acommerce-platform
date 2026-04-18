using ACommerce.Client.Operations;

namespace Ashare.V2.Web.Store;

/// <summary>
/// حالة Ashare.V2 — سليم عمداً (Slim). تتوسّع مع كل شريحة.
/// </summary>
public class AppStore : ITemplateStore
{
    public UiState Ui { get; } = new();
    public HashSet<string> FavoriteListingIds { get; } = new();

    // استعلامات البحث الأخيرة (آخر 10 فريدة — الأحدث أولاً)
    public List<string> RecentSearches { get; } = new();

    // فلاتر سريعة نشطة (near_me / low_price / top_rated)
    public HashSet<string> ActiveQuickFilterIds { get; } = new();

    public event Action? OnChanged;
    public void NotifyChanged() => OnChanged?.Invoke();

    public void AddRecentSearch(string q)
    {
        q = q.Trim();
        if (string.IsNullOrEmpty(q)) return;
        RecentSearches.Remove(q);
        RecentSearches.Insert(0, q);
        if (RecentSearches.Count > 10) RecentSearches.RemoveAt(RecentSearches.Count - 1);
        NotifyChanged();
    }
    public void RemoveRecentSearch(string q) { RecentSearches.Remove(q); NotifyChanged(); }
    public void ClearRecentSearches()         { RecentSearches.Clear();  NotifyChanged(); }

    // ── ITemplateStore ─────────────────────────────────────────────────────
    bool ITemplateStore.IsAuthenticated => false;
    Guid? ITemplateStore.UserId => null;
    string? ITemplateStore.AccessToken => null;
    string ITemplateStore.Theme => Ui.Theme;
    string ITemplateStore.Language => Ui.Language;
}

public class UiState
{
    public string Language { get; set; } = "ar";
    public string Theme { get; set; } = "light";
    public bool IsArabic => Language == "ar";
    public bool IsRtl => IsArabic;
}
