namespace ACommerce.Authentication.Operations.Abstractions;

/// <summary>
/// عقد اختياري يمكّن AuthService وAuthController من البحث عن المستخدمين
/// دون الاعتماد على نوع الكيان الخاص بكل تطبيق.
///
/// كل تطبيق يُطبق هذه الواجهة حسب نوع كيان المستخدم لديه،
/// مما يُحرر AuthController المُشترك من الاعتماد على نوع User محدد.
///
/// التسجيل:
///   services.AddScoped&lt;IUserLookup, MyAppUserLookup&gt;();
/// </summary>
public interface IUserLookup
{
    /// <summary>
    /// البحث عن مستخدم برقم الهاتف.
    /// يُرجع (UserId, DisplayName) إذا وُجد، أو null إذا لم يوجد.
    /// </summary>
    Task<(string UserId, string? DisplayName)?> FindByPhoneAsync(
        string phoneNumber,
        CancellationToken ct = default);

    /// <summary>
    /// البحث عن مستخدم بمعرفه.
    /// يُرجع (UserId, DisplayName) إذا وُجد، أو null إذا لم يوجد.
    /// </summary>
    Task<(string UserId, string? DisplayName)?> FindByIdAsync(
        string userId,
        CancellationToken ct = default);
}
