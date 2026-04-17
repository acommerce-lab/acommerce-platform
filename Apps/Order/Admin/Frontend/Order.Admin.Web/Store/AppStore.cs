using ACommerce.Client.Operations;

namespace Order.Admin.Web.Store;

public class AppStore : ITemplateStore
{
    public AuthState Auth { get; } = new();
    public UiState Ui { get; } = new();

    public event Action? OnChanged;
    public void NotifyChanged() => OnChanged?.Invoke();

    bool ITemplateStore.IsAuthenticated => Auth.IsAuthenticated;
    Guid? ITemplateStore.UserId => Auth.UserId;
    string? ITemplateStore.AccessToken => Auth.AccessToken;
    string ITemplateStore.Theme => Ui.Theme;
    string ITemplateStore.Language => Ui.Language;
}

public class AuthState
{
    public Guid? UserId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? DisplayName { get; set; }
    public string? AccessToken { get; set; }
    public bool IsAuthenticated => UserId.HasValue && !string.IsNullOrEmpty(AccessToken);

    public string? ChallengeId { get; set; }
    public Guid? PendingUserId { get; set; }
}

public class UiState
{
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "ar";
    public bool IsDark => Theme == "dark";
    public bool IsArabic => Language == "ar";
    public string Tr(string ar, string en) => IsArabic ? ar : en;
}
