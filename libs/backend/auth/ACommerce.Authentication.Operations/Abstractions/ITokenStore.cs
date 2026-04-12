namespace ACommerce.Authentication.Operations.Abstractions;

/// <summary>
/// عقد المزود: تخزين رموز التحديث والتحقق منها.
///
/// هذا مستوى L2 في LIBRARY-ANATOMY — واجهة مزود مطلوبة.
/// كل تطبيق يختار تنفيذه: ذاكرة، قاعدة بيانات، Redis، إلخ.
///
/// سجّل التنفيذ مع:
///   services.AddSingleton&lt;ITokenStore, MyTokenStore&gt;();
///
/// ثم أضف provider للـ AuthService:
///   builder.Requires&lt;ITokenStore&gt;()
/// </summary>
public interface ITokenStore
{
    /// <summary>تخزين رمز تحديث مرتبط بمستخدم.</summary>
    Task StoreRefreshTokenAsync(string refreshToken, string userId, CancellationToken ct = default);

    /// <summary>
    /// التحقق من رمز التحديث وإرجاع معرف المستخدم.
    /// يُرجع null إذا كان الرمز غير موجود أو منتهي الصلاحية.
    /// يُزيل الرمز تلقائياً (single-use).
    /// </summary>
    Task<string?> ValidateAndConsumeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>إلغاء كل رموز التحديث لمستخدم معين (تسجيل خروج من كل الأجهزة).</summary>
    Task RevokeAllAsync(string userId, CancellationToken ct = default);
}
