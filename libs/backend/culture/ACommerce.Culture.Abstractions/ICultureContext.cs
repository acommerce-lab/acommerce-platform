namespace ACommerce.Culture.Abstractions;

/// <summary>
/// السياق الثقافي الحالي — IANA timezone + primary language.
/// ASP.NET يعبّئه من الهيدر عبر CultureContextMiddleware؛
/// Blazor يعبّئه من المتصفح عبر خدمة BrowserCultureProbe.
/// </summary>
public interface ICultureContext
{
    /// <summary>IANA timezone (مثل "Asia/Riyadh", "Europe/London"). افتراضياً "UTC".</summary>
    string TimeZoneId { get; }
    /// <summary>لغة ISO-639 (مثل "ar", "en"). افتراضياً "ar".</summary>
    string Language { get; }
    /// <summary>نوع النظام الرقمي المستخدَم للعرض: "latin" | "arabic-indic" | "persian".</summary>
    string NumeralSystem { get; }
    /// <summary>المنطقة الزمنية كـ TimeZoneInfo (للحسابات).</summary>
    TimeZoneInfo TimeZone { get; }
}
