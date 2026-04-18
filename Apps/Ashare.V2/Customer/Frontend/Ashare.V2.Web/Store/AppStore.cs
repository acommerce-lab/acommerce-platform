using ACommerce.Client.Operations;

namespace Ashare.V2.Web.Store;

/// <summary>
/// حالة Ashare.V2 — توسّع مع كل شريحة.
/// </summary>
public class AppStore : ITemplateStore
{
    public UiState Ui { get; } = new();
    public AuthState Auth { get; } = new();
    public HashSet<string> FavoriteListingIds { get; } = new();
    public List<string> RecentSearches { get; } = new();
    public HashSet<string> ActiveQuickFilterIds { get; } = new();
    public DraftListing Draft { get; } = new();

    public event Action? OnChanged;
    public void NotifyChanged() => OnChanged?.Invoke();

    // ── Recent searches ───────────────────────────────────────────────
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

    // ── Preferences ───────────────────────────────────────────────────
    public void SetLanguage(string lang) { Ui.Language = lang; NotifyChanged(); }
    public void SetTheme(string theme)   { Ui.Theme = theme;   NotifyChanged(); }
    public void SetCity(string city)     { Ui.City = city;     NotifyChanged(); }

    // ── ITemplateStore ─────────────────────────────────────────────────
    bool ITemplateStore.IsAuthenticated => Auth.IsAuthenticated;
    Guid? ITemplateStore.UserId => Auth.UserId;
    string? ITemplateStore.AccessToken => Auth.AccessToken;
    string ITemplateStore.Theme => Ui.Theme;
    string ITemplateStore.Language => Ui.Language;
}

public class UiState
{
    public string Language { get; set; } = "ar";
    public string Theme { get; set; } = "light";
    public string City { get; set; } = "الرياض";
    public bool IsArabic => Language == "ar";
    public bool IsRtl => IsArabic;
    public bool IsDark => Theme == "dark";
}

public class AuthState
{
    public Guid? UserId { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? AccessToken { get; set; }
    public bool IsAuthenticated => UserId.HasValue && !string.IsNullOrEmpty(AccessToken);
}

/// <summary>مسودّة إعلان — تبقى حتى عند انتقال المستخدم للمصادقة.</summary>
public class DraftListing
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string TimeUnit { get; set; } = "month";
    public string? CategoryId { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public int Capacity { get; set; }
    public HashSet<string> Amenities { get; } = new();

    public void Clear()
    {
        Title = null; Description = null; Price = 0; TimeUnit = "month";
        CategoryId = null; City = null; District = null; Capacity = 0;
        Amenities.Clear();
    }
}
