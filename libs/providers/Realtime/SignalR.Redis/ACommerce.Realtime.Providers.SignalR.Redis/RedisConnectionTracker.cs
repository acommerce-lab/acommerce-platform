using ACommerce.Cache.Operations.Abstractions;
using ACommerce.Realtime.Operations.Abstractions;

namespace ACommerce.Realtime.Providers.SignalR.Redis;

/// <summary>
/// تطبيق <see cref="IConnectionTracker"/> فوق <see cref="ICache"/>. أيّ عمليّة backend
/// تستطيع البحث عن اتّصال مستخدم نشط على أيّ instance آخر — بشرط أنّ كلّها
/// تشير لنفس Redis.
///
/// <para>صيغة المفاتيح:</para>
/// <list type="bullet">
///   <item><c>realtime:conn:{userId}</c> → connectionId الحاليّ (آخر اتّصال). TTL ساعة افتراضيّاً.</item>
///   <item><c>realtime:presence:{userId}</c> → "1" إن مُتَّصل (TTL أقصر يُجدَّد بنبضة).</item>
/// </list>
///
/// <para>القيود: نفترض اتّصالاً واحداً للمستخدم. لو احتجت multi-device تُخزّن قائمة
/// (Redis Set) — تركتها بسيطة للحالة الشائعة.</para>
/// </summary>
public sealed class RedisConnectionTracker : IConnectionTracker
{
    private readonly ICache _cache;
    private static readonly TimeSpan ConnectionTtl = TimeSpan.FromHours(1);

    public RedisConnectionTracker(ICache cache) { _cache = cache; }

    public Task TrackConnectionAsync(string userId, string connectionId, CancellationToken ct = default)
        => _cache.SetAsync(ConnKey(userId), connectionId, CacheEntryOptions.Absolute(ConnectionTtl), ct);

    public Task RemoveConnectionAsync(string userId, CancellationToken ct = default)
        => _cache.RemoveAsync(ConnKey(userId), ct).ContinueWith(_ => { }, ct);

    public Task<string?> GetConnectionIdAsync(string userId, CancellationToken ct = default)
        => _cache.GetAsync<string>(ConnKey(userId), ct);

    public async Task<bool> IsOnlineAsync(string userId, CancellationToken ct = default)
        => await _cache.ExistsAsync(ConnKey(userId), ct);

    public async Task<Dictionary<string, bool>> GetPresenceAsync(IEnumerable<string> userIds, CancellationToken ct = default)
    {
        var result = new Dictionary<string, bool>();
        foreach (var u in userIds)
            result[u] = await _cache.ExistsAsync(ConnKey(u), ct);
        return result;
    }

    private static string ConnKey(string userId) => $"realtime:conn:{userId}";
}
