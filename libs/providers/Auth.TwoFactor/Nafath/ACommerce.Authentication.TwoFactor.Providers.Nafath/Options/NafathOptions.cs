namespace ACommerce.Authentication.TwoFactor.Providers.Nafath.Options;

/// <summary>
/// إعدادات Nafath (Absher).
/// </summary>
public class NafathOptions
{
    public const string SectionName = "Authentication:TwoFactor:Nafath";

    /// <summary>
    /// وضع التشغيل: "Sandbox" للاختبار، "Production" للإنتاج.
    /// </summary>
    public NafathMode Mode { get; set; } = NafathMode.Sandbox;

    /// <summary>معرف العميل لدى Nafath</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>سر العميل</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>اسم التطبيق الذي يظهر للمستخدم</summary>
    public string ApplicationName { get; set; } = "ACommerce";

    /// <summary>URL رئيسي لـ API</summary>
    public string BaseUrl { get; set; } = "https://api-sandbox.nafath.sa";

    /// <summary>مهلة HTTP</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>مدة صلاحية التحدي (عادة 5 دقائق)</summary>
    public TimeSpan ChallengeExpiration { get; set; } = TimeSpan.FromMinutes(5);
}

public enum NafathMode
{
    Sandbox,
    Production
}
