using ACommerce.Culture.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ACommerce.Culture.Interceptors;

/// <summary>
/// EF SaveChanges interceptor.  Before INSERT/UPDATE, walks every tracked
/// entity's string properties and replaces any Arabic-Indic / Persian digits
/// with their Latin equivalents.  This means: users may type a phone number
/// as "٩٦٦٥٠١١١١١١١" or "۰۵۰۱۱۱۱۱۱۱" but the canonical stored value is
/// always ASCII, so equality queries work and seed lookups never miss.
/// </summary>
public sealed class NumeralToLatinSaveInterceptor : SaveChangesInterceptor
{
    private readonly INumeralNormalizer _normalizer;
    public NumeralToLatinSaveInterceptor(INumeralNormalizer normalizer) => _normalizer = normalizer;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    { Normalize(eventData.Context); return base.SavingChanges(eventData, result); }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Normalize(eventData.Context); return base.SavingChangesAsync(eventData, result, cancellationToken); }

    private void Normalize(DbContext? ctx)
    {
        if (ctx is null) return;
        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added && entry.State != EntityState.Modified) continue;
            foreach (var prop in entry.Properties)
            {
                if (prop.Metadata.ClrType != typeof(string) || prop.CurrentValue is not string s) continue;
                if (!HasNonLatinDigits(s)) continue;
                prop.CurrentValue = _normalizer.ToLatin(s);
            }
        }
    }

    private static bool HasNonLatinDigits(string s)
    {
        foreach (var ch in s)
        {
            if ((ch >= '\u0660' && ch <= '\u0669') || (ch >= '\u06F0' && ch <= '\u06F9'))
                return true;
        }
        return false;
    }
}
