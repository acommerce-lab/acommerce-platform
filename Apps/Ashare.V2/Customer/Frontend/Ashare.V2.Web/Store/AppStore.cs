using ACommerce.Client.Operations;

namespace Ashare.V2.Web.Store;

/// <summary>
/// حالة Ashare.V2 — شريحة Home فقط. Slim عمداً: لا Auth، لا Cart.
/// سنُوسّعها مع كل شريحة أفقية تالية.
/// </summary>
public class AppStore : ITemplateStore
{
    public UiState Ui { get; } = new();
    public HashSet<string> FavoriteListingIds { get; } = new();

    public event Action? OnChanged;
    public void NotifyChanged() => OnChanged?.Invoke();

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
