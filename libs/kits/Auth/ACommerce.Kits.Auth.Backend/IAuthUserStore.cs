namespace ACommerce.Kits.Auth.Backend;

/// <summary>
/// منفذ المستخدمين الذي يربط <see cref="AuthController"/> ببيانات الـ app.
/// التطبيق يكتب نسخة بسيطة تطابق طبقة بياناته (in-memory seed، EF Users…).
/// </summary>
public interface IAuthUserStore
{
    /// <summary>يرجع <c>userId</c> المرتبط بالهاتف، أو ينشئه إن لم يوجد.</summary>
    Task<string> GetOrCreateUserIdAsync(string phone, CancellationToken ct);

    /// <summary>اسم العرض للمستخدم (FullName) أو فارغ.</summary>
    Task<string?> GetDisplayNameAsync(string userId, CancellationToken ct);
}

/// <summary>
/// خيارات الـ JWT التي يقصّها <see cref="AuthController"/> عند إصدار التوكن.
/// كلّ تطبيق يحقن نسخته بقيم issuer/audience/secret/role الخاصّة به.
/// </summary>
public sealed record AuthKitJwtConfig(
    string Secret,
    string Issuer,
    string Audience,
    string Role,                                // مثلاً "user"، "provider"، "admin"، "vendor"
    string PartyKind = "User",                  // بادئة الـ realtime party id
    int    AccessTokenLifetimeDays = 30);
