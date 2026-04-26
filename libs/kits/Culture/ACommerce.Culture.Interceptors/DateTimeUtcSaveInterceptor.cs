using ACommerce.Culture.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ACommerce.Culture.Interceptors;

/// <summary>
/// EF SaveChanges interceptor that guarantees every DateTime / DateTime?
/// column is stored as UTC.  Any Local/Unspecified value is re-interpreted
/// in the current user's timezone (ICultureContext) and converted to UTC.
/// </summary>
public sealed class DateTimeUtcSaveInterceptor : SaveChangesInterceptor
{
    private readonly IDateTimeNormalizer _dt;
    private readonly ICultureContext _culture;

    public DateTimeUtcSaveInterceptor(IDateTimeNormalizer dt, ICultureContext culture)
    { _dt = dt; _culture = culture; }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    { Normalize(eventData.Context); return base.SavingChanges(eventData, result); }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Normalize(eventData.Context); return base.SavingChangesAsync(eventData, result, cancellationToken); }

    private void Normalize(DbContext? ctx)
    {
        if (ctx is null) return;
        var tz = _culture.TimeZone;
        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added && entry.State != EntityState.Modified) continue;
            foreach (var prop in entry.Properties)
            {
                var clr = prop.Metadata.ClrType;
                if (clr == typeof(DateTime))
                {
                    var v = (DateTime)prop.CurrentValue!;
                    if (v.Kind != DateTimeKind.Utc)
                        prop.CurrentValue = _dt.ToUtc(v, tz);
                }
                else if (clr == typeof(DateTime?))
                {
                    if (prop.CurrentValue is DateTime v && v.Kind != DateTimeKind.Utc)
                        prop.CurrentValue = _dt.ToUtc(v, tz);
                }
            }
        }
    }
}
