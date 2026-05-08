using ACommerce.Culture.Abstractions;

namespace ACommerce.Culture.Defaults;

/// <summary>
/// سِياق ثَقافيّ افتراضيّ ثابِت. الافتراضات: Riyadh timezone، ar، arabic-indic، SAR.
/// </summary>
public sealed class StaticCultureContext : ICultureContext
{
    public StaticCultureContext(string timeZoneId = "Asia/Riyadh",
                                string language = "ar",
                                string numeralSystem = "arabic-indic",
                                string currency = "SAR")
    {
        TimeZoneId = timeZoneId;
        Language = language;
        NumeralSystem = numeralSystem;
        Currency = currency;
        TimeZone = ResolveTz(timeZoneId);
    }

    public string TimeZoneId { get; }
    public string Language { get; }
    public string NumeralSystem { get; }
    public TimeZoneInfo TimeZone { get; }
    public string Currency { get; }

    public static TimeZoneInfo ResolveTz(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return TimeZoneInfo.Utc; }
    }
}

/// <summary>
/// نُسخة قابِلة لِلكِتابة (Scoped) — middleware أو خَدَمة Blazor تَكتُب إليها،
/// وكلّ ما يَعتَمِد عَلى ICultureContext يَقرأ مِنها.
/// </summary>
public sealed class MutableCultureContext : ICultureContext
{
    private TimeZoneInfo _tz = TimeZoneInfo.Utc;

    public string TimeZoneId    { get; private set; } = "UTC";
    public string Language      { get; private set; } = "ar";
    public string NumeralSystem { get; private set; } = "arabic-indic";
    public string Currency      { get; private set; } = "SAR";
    public TimeZoneInfo TimeZone => _tz;

    public void Set(string? tz, string? lang, string? numerals, string? currency = null)
    {
        if (!string.IsNullOrWhiteSpace(tz))       { TimeZoneId = tz;       _tz = StaticCultureContext.ResolveTz(tz); }
        if (!string.IsNullOrWhiteSpace(lang))     Language = lang;
        if (!string.IsNullOrWhiteSpace(numerals)) NumeralSystem = numerals;
        if (!string.IsNullOrWhiteSpace(currency)) Currency = currency;
    }
}
