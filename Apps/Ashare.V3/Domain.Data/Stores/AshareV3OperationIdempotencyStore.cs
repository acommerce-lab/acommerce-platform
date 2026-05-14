using ACommerce.OperationEngine.Interceptors;
using Ashare.V3.Data;
using Ashare.V3.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ashare.V3.Data.Stores;

/// <summary>
/// تَنفيذ <see cref="IOperationIdempotencyStore"/> فَوق EF Core لِـ V3.
/// نَفس النَّمَط في Ejar — جَدول <c>OperationIdempotency</c> + Key فَريد.
/// </summary>
public sealed class AshareV3OperationIdempotencyStore : IOperationIdempotencyStore
{
    private readonly AshareV3DbContext _db;
    public AshareV3OperationIdempotencyStore(AshareV3DbContext db) => _db = db;

    public async Task<IdempotencyRecord?> TryGetAsync(string key, CancellationToken ct)
    {
        var row = await _db.OperationIdempotency.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, ct);
        if (row is null) return null;
        return new IdempotencyRecord(row.Key, row.OperationType, row.Snapshot, row.CreatedAt);
    }

    public async Task SaveAsync(string key, string operationType, string snapshot, CancellationToken ct)
    {
        try
        {
            _db.OperationIdempotency.Add(new OperationIdempotencyEntity
            {
                Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
                Key = key, OperationType = operationType, Snapshot = snapshot,
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) { /* unique constraint = key مَوجود ⇒ تَجاوَز */ }
    }
}
