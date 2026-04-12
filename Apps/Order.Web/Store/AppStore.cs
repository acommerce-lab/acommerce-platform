namespace Order.Web.Store;

/// <summary>
/// حالة التطبيق الكاملة — حاوية واحدة تُحدَّث عبر مُفسّرات العمليات.
/// لا خدمات منفصلة. كل تغيير حالة = عملية → مُفسّر → تحديث هنا.
/// </summary>
public class AppStore
{
    public AuthState Auth { get; } = new();
    public CartState Cart { get; } = new();
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

    // OTP flow intermediate state
    public string? ChallengeId { get; set; }
    public Guid? PendingUserId { get; set; }
}

// ── Cart ──────────────────────────────────────────────────────────────────
public class CartState
{
    public List<CartItem> Items { get; } = new();
    public Guid? VendorId { get; set; }
    public string? VendorName { get; set; }
    public string? VendorEmoji { get; set; }
    public int Count => Items.Sum(i => i.Quantity);
    public decimal Subtotal => Items.Sum(i => i.UnitPrice * i.Quantity);
}

public class CartItem
{
    public Guid OfferId { get; set; }
    public string Title { get; set; } = "";
    public string Emoji { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
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
