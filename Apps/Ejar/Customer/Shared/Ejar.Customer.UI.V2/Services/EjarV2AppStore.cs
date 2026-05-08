using System.Net.Http.Headers;

namespace Ejar.Customer.UI.V2.Services;

/// <summary>
/// حالة V2 المَحلّيّة: Auth (JWT + UserId + الاسم + الهاتف) + إعدادات
/// واجهة بسيطة. <b>لا</b> تَحوي مَنطق الكيتس — كلّ kit يُدير حالته
/// عبر IXxxStore الخاص به. هذا فقط لتَخزين الـ JWT.
/// </summary>
public sealed class EjarV2AppStore
{
    public AuthState Auth { get; } = new();
    public event Action? OnChanged;
    public void NotifyChanged() => OnChanged?.Invoke();
}

public sealed class AuthState
{
    public Guid?  UserId      { get; set; }
    public string? FullName   { get; set; }
    public string? Phone      { get; set; }
    public string? AccessToken{ get; set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && UserId is not null;
}
