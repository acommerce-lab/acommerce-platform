namespace ACommerce.Kits.Auth.Operations;

/// <summary>
/// منفذ المستخدمين الذي يربط الـ Auth flow ببيانات الـ app. التطبيق يكتب
/// نسخة بسيطة تطابق طبقة بياناته (in-memory seed، EF Users…).
/// </summary>
public interface IAuthUserStore
{
    /// <summary>يرجع <c>userId</c> المرتبط بالـ subject (هاتف/بريد/معرّف وطنيّ)،
    /// أو ينشئه إن لم يوجد. يُستدعى في خطوة الإكمال.</summary>
    Task<string> GetOrCreateUserIdAsync(string subject, CancellationToken ct);

    /// <summary>اسم العرض للمستخدم (FullName) أو فارغ.</summary>
    Task<string?> GetDisplayNameAsync(string userId, CancellationToken ct);
}

/// <summary>
/// خيارات الـ JWT التي يستعملها الـ AuthController عند إصدار التوكن بعد
/// إكمال الـ flow بنجاح.
/// </summary>
public sealed record AuthKitJwtConfig(
    string Secret,
    string Issuer,
    string Audience,
    string Role,                                // مثلاً "user"، "provider"، "admin"، "vendor"
    string PartyKind = "User",                  // بادئة الـ realtime party id
    int    AccessTokenLifetimeDays = 30);
