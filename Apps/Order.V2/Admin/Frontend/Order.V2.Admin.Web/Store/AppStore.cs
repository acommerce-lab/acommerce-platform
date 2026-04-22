using ACommerce.Client.Operations;

namespace Order.V2.Admin.Web.Store;

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
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AccessToken { get; set; }
    public string? ChallengeId { get; set; }
    public Guid? PendingUserId { get; set; }
    public bool IsAuthenticated => UserId.HasValue && !string.IsNullOrEmpty(AccessToken);
}

public class UiState
{
    public string Theme { get; set; } = "dark";
    public string Language { get; set; } = "ar";
}
