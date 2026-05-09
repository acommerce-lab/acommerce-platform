namespace ACommerce.Payments.Providers.Noon.Options;

/// <summary>
/// إعدادات Noon Payments.
/// </summary>
public class NoonOptions
{
    public const string SectionName = "Payments:Noon";

    /// <summary>معرف التاجر في Noon (Business Identifier)</summary>
    public string BusinessIdentifier { get; set; } = string.Empty;

    /// <summary>مفتاح التطبيق (Application Identifier)</summary>
    public string ApplicationIdentifier { get; set; } = string.Empty;

    /// <summary>مفتاح API (API Key)</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>وضع Noon: Test أو Live</summary>
    public NoonMode Mode { get; set; } = NoonMode.Test;

    /// <summary>العملة الافتراضية</summary>
    public string DefaultCurrency { get; set; } = "SAR";

    /// <summary>
    /// فئة العملية: "AUTHORIZE" (تفويض ثم التقاط) أو "SALE" (دفع مباشر).
    /// </summary>
    public string Category { get; set; } = "SALE";

    /// <summary>مهلة HTTP</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// قناة الدفع (Channel).
    /// القيم: "Web", "Mobile", "PayByLink", "POS", "Invoice"
    /// </summary>
    public string Channel { get; set; } = "Web";

    /// <summary>سر التحقق من webhook</summary>
    public string? WebhookSecret { get; set; }

    public string GetBaseUrl() => Mode switch
    {
        NoonMode.Live => "https://api.noonpayments.com",
        _ => "https://api-test.noonpayments.com"
    };
}

public enum NoonMode
{
    Test,
    Live
}
