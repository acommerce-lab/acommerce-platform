using ACommerce.OperationEngine.Interceptors;
using Ejar.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Ejar.Api.Stores;

/// <summary>
/// تَنفيذ <see cref="IOperationIdempotencyStore"/> فَوق EF Core. يُستَخدَم
/// مَن <see cref="IdempotencyInterceptor"/> لِفَحص + تَسجيل أَيّ
/// idempotency_key مَن الكلاينت.
///
/// <para><b>TTL</b>: لا حَذف تِلقائي بَعد. الجَدول يَنمو خَطّياً ⇒ TODO:
/// background job يَحذِف &gt; ٢٤ ساعَة.</para>
/// </summary>
public sealed class EjarOperationIdempotencyStore : IOperationIdempotencyStore
{
    private readonly EjarDbContext _db;
    public EjarOperationIdempotencyStore(EjarDbContext db) => _db = db;

    public async Task<IdempotencyRecord?> TryGetAsync(string key, CancellationToken ct)
    {
        var row = await _db.OperationIdempotency.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, ct);
        if (row is null) return null;
        return new IdempotencyRecord(row.Key, row.OperationType, row.Snapshot, row.CreatedAt);
    }

    public async Task SaveAsync(string key, string operationType, string snapshot, CancellationToken ct)
    {
        // Race-safe: لَو سَطر بِنَفس الـ Key أُدخِل بَين TryGet + Save،
        // unique index سَيَرفُض. نَتَجاهَل الـ DbUpdateException في تِلك الحالَة.
        try
        {
            _db.OperationIdempotency.Add(new OperationIdempotencyEntity
            {
                Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
                Key = key, OperationType = operationType, Snapshot = snapshot,
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // duplicate ⇒ key مُسَجَّل سَلَفاً بِواسِطَة طَلَب آخَر، لا داعي لِلتَكرار.
        }
    }
}
