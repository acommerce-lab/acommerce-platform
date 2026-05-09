using ACommerce.Client.Operations;
using ACommerce.ClientHost.Auth;
using ACommerce.Culture.Abstractions;
using ACommerce.Culture.Defaults;
using ACommerce.L10n.Blazor;

namespace Ejar.Customer.UI.Store;

/// <summary>
/// AppStore — V1 application state. بَعد F57: AuthState فاضول façade فوق
/// <see cref="IClientAuthState"/>. بَعد F59: <see cref="UiState"/> يَكشِف
/// <see cref="ICultureContext"/> عَبر adapter داخِليّ، وَ <see cref="L10n.Blazor.ILanguageContext"/>
/// يَقرأ مِنه. كلّ مَنطِق Auth + Culture + L10n مُوَحَّد عَبر تَجريدات الكيتس.
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

    public void SetCulture(EjarUserCulture c) { Ui.Culture = c; NotifyChanged(); }
    public void SetTheme(string theme)        { Ui.Theme = theme; NotifyChanged(); }
    public void SetCity(string city)          { Ui.City = city;   NotifyChanged(); }

    // ── ITemplateStore ─────────────────────────────────────────────────
    bool ITemplateStore.IsAuthenticated => Auth.IsAuthenticated;
    Guid? ITemplateStore.UserId => Auth.UserId;
    string? ITemplateStore.AccessToken => Auth.AccessToken;
    string ITemplateStore.Theme => Ui.Theme;
    string ITemplateStore.Language => Ui.Culture.Language;
}

public class UiState
{
    public EjarUserCulture Culture { get; set; } = EjarUserCulture.Default;
    public string Theme { get; set; } = "light";
    public string City { get; set; } = "إب";

    public string Language => Culture.Language;
    public bool IsArabic => Culture.Language == "ar";
    public bool IsRtl => IsArabic;
    public bool IsDark => Theme == "dark";
    public bool HideChrome { get; set; }
}

/// <summary>POCO V1: language + timezone + currency. اسم EjarUserCulture لِتَجَنُّب تَعارُض مَع UserCulture المَحذوف.</summary>
public sealed record EjarUserCulture(string Language, string TimeZone, string Currency)
{
    public static EjarUserCulture Default => new("ar", "Asia/Aden", "YER");
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

/// <summary>
/// adapter يَكشِف <c>AppStore.Ui</c> كَ <see cref="ICultureContext"/> + <see cref="ILanguageContext"/>
/// لِيَستَهلِكها <c>CultureHeadersHandler</c> + <c>CultureInterceptor</c> + <c>L</c> (مِن L10n.Blazor)
/// + أيّ مُكَوِّن آخَر يَعتَمِد عَلى الكيتس.
/// </summary>
public sealed class AppStoreCultureContext : ICultureContext, ILanguageContext
{
    private readonly AppStore _store;
    public AppStoreCultureContext(AppStore store) => _store = store;

    public string TimeZoneId    => _store.Ui.Culture.TimeZone;
    public string Language      => _store.Ui.Culture.Language;
    public string NumeralSystem => _store.Ui.Culture.Language == "ar" ? "arabic-indic" : "latin";
    public TimeZoneInfo TimeZone => StaticCultureContext.ResolveTz(_store.Ui.Culture.TimeZone);
    public string Currency      => _store.Ui.Culture.Currency;
    public bool   IsRtl         => _store.Ui.IsRtl;
}
