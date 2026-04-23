namespace Ashare.V2.Web.Store;

/// <summary>
/// ثقافة المستخدم: لغة + منطقة زمنيّة + عملة. تتغيّر كوحدة واحدة عبر
/// <c>ui.set_culture</c>، ويفرضها <c>CultureInterceptor</c> في الذهاب
/// (رؤوس HTTP) والإياب (تحويل DateTime/Money/Translatable في الحمولات).
///
/// لماذا record؟ لنحصل على <c>with</c> للتغيير الجزئيّ دون كتابة موادّ setter.
/// لماذا بنفسها لا داخل UiState؟ لتسهيل تمريرها كوحدة عبر DelegatingHandler.
/// </summary>
public sealed record UserCulture(string Language, string TimeZone, string Currency)
{
    /// <summary>السعوديّة الافتراضيّة: عربيّة، الرياض، ريال.</summary>
    public static UserCulture Default => new("ar", "Asia/Riyadh", "SAR");
}
