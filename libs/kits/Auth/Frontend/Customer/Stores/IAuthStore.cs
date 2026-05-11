namespace ACommerce.Kits.Auth.Frontend.Customer.Stores;

/// <summary>
/// store reactive لحالة الـ Auth على العميل. التطبيق يَربطه بمصدر JWT
/// (storage محلّيّ + refresh token). صفحات الـ Auth kit تَستهلك هذه
/// الواجهة فقط — لا تَلمس JS storage مباشرةً.
/// </summary>
public interface IAuthStore
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? FullName { get; }
    bool IsBusy { get; }
    string? LastError { get; }
    event Action? Changed;

    /// <summary>
    /// مُعَرِّف التَّحَدّي الَّذي أَرجَعَه backend بَعد آخِر RequestOtp ناجِح.
    /// تَستَخدِمه واجِهات الـ flows الَّتي تَحتاج مُتابَعَة التَّحَدّي
    /// (مَثَلاً Nafath polling). null لَو لَم يَبدَأ تَحَدٍّ أَو فَشَل.
    /// </summary>
    string? LastChallengeId { get; }

    /// <summary>
    /// بَيانات الـ provider لِآخِر تَحَدٍّ (مَثَلاً <c>displayCode</c> لِنَفاذ،
    /// <c>autoVerifyInSeconds</c>، <c>masked</c> لِـ SMS). الـ store لا يَتَفَهَّمها —
    /// يُمَرِّرها كَما هي لِواجِهَة الـ flow المُختارَة.
    /// </summary>
    IReadOnlyDictionary<string, string>? LastProviderData { get; }

    /// <summary>يَطلب OTP للهاتف. يَنجح ⇒ <see cref="LastError"/> = null.</summary>
    Task RequestOtpAsync(string phone, CancellationToken ct = default);

    /// <summary>يتحقّق من OTP ويَستلم JWT. يَنجح ⇒ <see cref="IsAuthenticated"/> = true.</summary>
    Task VerifyOtpAsync(string phone, string code, CancellationToken ct = default);

    Task LogoutAsync(CancellationToken ct = default);
}
