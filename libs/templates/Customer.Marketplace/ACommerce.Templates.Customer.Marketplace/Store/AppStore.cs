using ACommerce.Client.Operations;
using ACommerce.ClientHost.Auth;

namespace Ejar.Customer.UI.Store;

/// <summary>
/// AppStore — V1 application state. بَعد F57: AuthState صار façade فوق
/// <see cref="IClientAuthState"/> (مِن ClientHost.Auth). تَخزين JWT
/// + Bearer header + AuthenticationStateProvider كلّها مِن ClientHost الآن.
/// V1 pages تَستَمِرّ في كتابة <c>Store.Auth.UserId = ...</c> و
/// <c>Store.NotifyChanged()</c> بدون تَغيير.
/// </summary>
public class AppStore : ITemplateStore
{
    public UiState Ui { get; } = new();
    public AuthState Auth { get; }
    public HashSet<string> FavoriteListingIds { get; } = new();
    public List<string> RecentSearches { get; } = new();
    public HashSet<string> ActiveQuickFilterIds { get; } = new();
    public DraftListing Draft { get; } = new();

    public event Action? OnChanged;

    /// <summary>
    /// يُعلِم المُستَهلِكين + ClientHost.Auth (لأنّ AuthenticatedHttpClient
    /// يَتَزامَن مَع IClientAuthState.OnChanged فيُحَدِّث Bearer header فَوراً).
    /// </summary>
    public void NotifyChanged()
    {
        _authState.NotifyChanged();
        OnChanged?.Invoke();
    }

    private readonly IClientAuthState _authState;

    public AppStore(IClientAuthState authState)
    {
        _authState = authState;
        Auth = new AuthState(authState);
    }

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
    public void ClearRecentSearches()        { RecentSearches.Clear();  NotifyChanged(); }

    public void SetCulture(UserCulture c) { Ui.Culture = c; NotifyChanged(); }
    public void SetTheme(string theme)    { Ui.Theme = theme; NotifyChanged(); }
    public void SetCity(string city)      { Ui.City = city;   NotifyChanged(); }

    // ── ITemplateStore ─────────────────────────────────────────────────
    bool ITemplateStore.IsAuthenticated => Auth.IsAuthenticated;
    Guid? ITemplateStore.UserId => Auth.UserId;
    string? ITemplateStore.AccessToken => Auth.AccessToken;
    string ITemplateStore.Theme => Ui.Theme;
    string ITemplateStore.Language => Ui.Culture.Language;
}

public class UiState
{
    public UserCulture Culture { get; set; } = UserCulture.Default;
    public string Theme { get; set; } = "light";
    public string City { get; set; } = "إب";

    public string Language => Culture.Language;
    public bool IsArabic => Culture.Language == "ar";
    public bool IsRtl => IsArabic;
    public bool IsDark => Theme == "dark";
    public bool HideChrome { get; set; }
}

/// <summary>façade فوق IClientAuthState — V1 pages تَستَخدِمه كَأنّه AuthState قَديم.</summary>
public class AuthState
{
    private readonly IClientAuthState _state;
    public AuthState(IClientAuthState state) => _state = state;

    public Guid?  UserId      { get => _state.UserId;      set => _state.UserId = value; }
    public string? FullName   { get => _state.FullName;    set => _state.FullName = value; }
    public string? Phone      { get => _state.Phone;       set => _state.Phone = value; }
    public string? AccessToken{ get => _state.AccessToken; set => _state.AccessToken = value; }
    public bool   IsAuthenticated => _state.IsAuthenticated;
}

public class DraftListing
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string TimeUnit { get; set; } = "monthly";
    public string? CategoryId { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public int BedroomCount { get; set; }
    public HashSet<string> Amenities { get; } = new();

    public void Clear()
    {
        Title = null; Description = null; Price = 0; TimeUnit = "monthly";
        CategoryId = null; City = null; District = null;
        BedroomCount = 0; Amenities.Clear();
    }
}
