namespace ACommerce.Cache.Operations.Abstractions;

/// <summary>
/// قفل موزَّع مكتسَب من <see cref="ICache.AcquireLockAsync"/>. التخلّص يحرّر
/// القفل بأمان (idempotent). إن انتهى TTL قبل التخلّص، يحرَّر تلقائيّاً.
///
/// <para>الاستخدام النموذجيّ:</para>
/// <code>
/// await using var l = await _cache.AcquireLockAsync("payment:" + orderId, TimeSpan.FromSeconds(30));
/// if (l is null) return Conflict();
/// // critical section ...
/// </code>
/// </summary>
public interface IDistributedLock : IAsyncDisposable
{
    string Key { get; }
}
