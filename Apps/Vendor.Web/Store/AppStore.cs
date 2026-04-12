using ACommerce.Client.Operations;

namespace Vendor.Web.Store;

/// <summary>
/// حالة التطبيق الكاملة — حاوية واحدة تُحدَّث عبر مُفسّرات العمليات.
/// لا خدمات منفصلة. كل تغيير حالة = عملية → مُفسّر → تحديث هنا.
/// يطبّق ITemplateStore حتى تتمكن القوالب المحايدة من الوصول للحالة.
/// </summary>
public class AppStore : ITemplateStore
{
    public AuthState Auth { get; } = new();
    public UiState Ui { get; } = new();

    public event Action? OnChanged;
    public void NotifyChanged() => OnChanged?.Invoke();

    // ── ITemplateStore ─────────────────────────────────────────────────────
    bool ITemplateStore.IsAuthenticated => Auth.IsAuthenticated;
    Guid? ITemplateStore.UserId => Auth.UserId;
    string? ITemplateStore.AccessToken => Auth.AccessToken;
    string ITemplateStore.Theme => Ui.Theme;
    string ITemplateStore.Language => Ui.Language;
}

// ── Auth ──────────────────────────────────────────────────────────────────
public class AuthState
{
    public Guid? UserId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FullName { get; set; }
    public string? AccessToken { get; set; }
    public bool IsAuthenticated => UserId.HasValue && !string.IsNullOrEmpty(AccessToken);

    // OTP flow intermediate state
    public string? ChallengeId { get; set; }
    public Guid? PendingUserId { get; set; }
}

// ── UI preferences ───────────────────────────────────────────────────────
public class UiState
{
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "ar";
    public bool IsDark => Theme == "dark";
    public bool IsArabic => Language == "ar";
    public string Tr(string ar, string en) => IsArabic ? ar : en;
}
