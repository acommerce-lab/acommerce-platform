using ACommerce.Client.Operations;

namespace Order.V2.Web.Store;

public class AppStore : ITemplateStore
{
    public AuthState Auth { get; } = new();
    public CartState Cart { get; } = new();
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
    public string? FullName { get; set; }
    public string? AccessToken { get; set; }
    public bool IsAuthenticated => UserId.HasValue && !string.IsNullOrEmpty(AccessToken);

    public string? ChallengeId { get; set; }
    public Guid? PendingUserId { get; set; }
}

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

public class UiState
{
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "ar";
    public bool IsDark => Theme == "dark";
    public bool IsArabic => Language == "ar";
    public string Tr(string ar, string en) => IsArabic ? ar : en;
}
