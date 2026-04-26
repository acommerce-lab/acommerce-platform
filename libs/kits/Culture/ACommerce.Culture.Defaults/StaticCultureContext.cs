using ACommerce.Culture.Abstractions;

namespace ACommerce.Culture.Defaults;

/// <summary>
/// سياق ثقافي افتراضي ثابت — يُستخدم قبل أن يُعبِّئه middleware أو فرونت-إند.
/// الافتراضات: Riyadh timezone, ar, arabic-indic.
/// </summary>
public sealed class StaticCultureContext : ICultureContext
{
    public StaticCultureContext(string timeZoneId = "Asia/Riyadh",
                                string language = "ar",
                                string numeralSystem = "arabic-indic")
    {
        TimeZoneId = timeZoneId;
        Language = language;
        NumeralSystem = numeralSystem;
        TimeZone = ResolveTz(timeZoneId);
    }

    public string TimeZoneId { get; }
    public string Language { get; }
    public string NumeralSystem { get; }
    public TimeZoneInfo TimeZone { get; }

    public static TimeZoneInfo ResolveTz(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return TimeZoneInfo.Utc; }
    }
}

/// <summary>
/// نسخة قابلة للكتابة (Scoped في DI) يكتب إليها الميدل-وير أو خدمة Blazor،
/// ويقرأها كل ما يعتمد على ICultureContext.
/// </summary>
public sealed class MutableCultureContext : ICultureContext
{
    private TimeZoneInfo _tz = TimeZoneInfo.Utc;

    public string TimeZoneId { get; private set; } = "UTC";
    public string Language { get; private set; } = "ar";
    public string NumeralSystem { get; private set; } = "arabic-indic";
    public TimeZoneInfo TimeZone => _tz;

    public void Set(string? tz, string? lang, string? numerals)
    {
        if (!string.IsNullOrWhiteSpace(tz))       { TimeZoneId = tz;       _tz = StaticCultureContext.ResolveTz(tz); }
        if (!string.IsNullOrWhiteSpace(lang))     Language = lang;
        if (!string.IsNullOrWhiteSpace(numerals)) NumeralSystem = numerals;
    }
}
