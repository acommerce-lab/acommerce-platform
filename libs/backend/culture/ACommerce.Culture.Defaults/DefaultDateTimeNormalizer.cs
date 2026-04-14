using ACommerce.Culture.Abstractions;

namespace ACommerce.Culture.Defaults;

public sealed class DefaultDateTimeNormalizer : IDateTimeNormalizer
{
    public DateTime ToUtc(DateTime local, TimeZoneInfo tz)
    {
        // If already flagged UTC, trust it.
        if (local.Kind == DateTimeKind.Utc) return local;
        // If unspecified, assume it's in the given tz.
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
    }

    public DateTime ToLocal(DateTime utc, TimeZoneInfo tz)
    {
        var asUtc = utc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utc, DateTimeKind.Utc)
            : utc.ToUniversalTime();
        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, tz);
    }

    public DateTime FromOffsetToUtc(DateTimeOffset dto) => dto.UtcDateTime;
}
