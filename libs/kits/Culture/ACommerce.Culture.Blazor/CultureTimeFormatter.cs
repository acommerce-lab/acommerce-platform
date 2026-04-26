using ACommerce.Culture.Abstractions;

namespace ACommerce.Culture.Blazor;

/// <summary>
/// Short helpers that Razor pages call to render a UTC DateTime in the
/// current user's timezone — used inside chat, order-list, etc.
/// Apps inject this service and format with `.AsLocal(dt, "HH:mm")`.
/// </summary>
public sealed class CultureTimeFormatter
{
    private readonly ICultureContext _ctx;
    private readonly IDateTimeNormalizer _dt;

    public CultureTimeFormatter(ICultureContext ctx, IDateTimeNormalizer dt)
    { _ctx = ctx; _dt = dt; }

    public DateTime AsLocal(DateTime utc) => _dt.ToLocal(utc, _ctx.TimeZone);

    public string AsLocal(DateTime utc, string format)
        => _dt.ToLocal(utc, _ctx.TimeZone).ToString(format);

    public string AsLocalShort(DateTime utc) => AsLocal(utc, "HH:mm");
}
