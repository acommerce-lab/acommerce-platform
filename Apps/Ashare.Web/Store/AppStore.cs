namespace Ashare.Web.Store;

/// <summary>
/// حالة التطبيق الكاملة لـ Ashare.Web — حاوية واحدة تُحدَّث عبر مُفسّرات العمليات.
/// لا خدمات منفصلة. كل تغيير حالة = عملية → مُفسّر → تحديث هنا.
/// </summary>
public class AppStore
{
    public AuthState Auth { get; } = new();
    public UiState Ui { get; } = new();

    public event Action? OnChanged;
    public void NotifyChanged() => OnChanged?.Invoke();
}

// ── Auth ──────────────────────────────────────────────────────────────────
public class AuthState
{
    public Guid? UserId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FullName { get; set; }
    public string? AccessToken { get; set; }
    public bool IsAuthenticated => UserId.HasValue && !string.IsNullOrEmpty(AccessToken);

    // حالة مؤقتة لتدفق OTP
    public string? ChallengeId { get; set; }
    public Guid? PendingUserId { get; set; }
}

// ── تفضيلات UI ───────────────────────────────────────────────────────────
public class UiState
{
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "ar";
    public bool IsDark => Theme == "dark";
    public bool IsArabic => Language == "ar";
    public string Tr(string ar, string en) => IsArabic ? ar : en;
}
