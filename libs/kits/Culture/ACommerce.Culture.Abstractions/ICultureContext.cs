namespace ACommerce.Culture.Abstractions;

/// <summary>
/// السِياق الثَقافيّ الحاليّ — IANA timezone + primary language + currency.
/// ASP.NET يُعَبِّئه مِن الهيدر عَبر CultureContextMiddleware؛ Blazor يُعَبِّئه
/// مِن المُتَصَفِّح أو AppStore الخاصّ بالتَطبيق.
/// </summary>
public interface ICultureContext
{
    /// <summary>IANA timezone (مَثلاً "Asia/Riyadh", "Europe/London"). افتراضيّاً "UTC".</summary>
    string TimeZoneId { get; }
    /// <summary>لُغة ISO-639 (مَثلاً "ar", "en"). افتراضيّاً "ar".</summary>
    string Language { get; }
    /// <summary>نَوع النِظام الرَقَميّ لِلعَرض: "latin" | "arabic-indic" | "persian".</summary>
    string NumeralSystem { get; }
    /// <summary>المِنطَقَة الزَمَنيّة كَـ TimeZoneInfo (لِلحِسابات).</summary>
    TimeZoneInfo TimeZone { get; }
    /// <summary>عُملَة المُستَخدِم بِصيغَة ISO-4217 (مَثلاً "SAR", "YER", "USD"). افتراضيّاً "SAR".</summary>
    string Currency { get; }
}
