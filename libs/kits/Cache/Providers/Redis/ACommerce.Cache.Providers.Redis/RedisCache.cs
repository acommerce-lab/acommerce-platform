using System.Text.Json;
using ACommerce.Cache.Operations.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ACommerce.Cache.Providers.Redis;

/// <summary>
/// تطبيق <see cref="ICache"/> فوق Redis. يستعمل JSON للتسلسل عدا للـ <c>string</c>
/// و <c>byte[]</c> (يكتبهما خام). الأقفال مبنيّة على <c>SET ... NX PX</c> مع
/// قيمة GUID مخفيّة، والتحرير عبر سكريبت Lua آمن (يقارن قبل الحذف).
/// </summary>
public sealed class RedisCache : ICache
{
    // Lua: حذف فقط إن قيمة المفتاح تساوي القيمة المعطاة (للأقفال).
    private const string ReleaseLockScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

    private readonly IDatabase _db;
    private readonly ILogger<RedisCache> _logger;

    public RedisCache(IConnectionMultiplexer mux, ILogger<RedisCache> logger)
    {
        _db = mux.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var raw = await _db.StringGetAsync(key);
        if (raw.IsNullOrEmpty) return default;
        return Deserialize<T>(raw!);
    }

    public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken ct = default)
    {
        var ttl = ResolveTtl(options);
        // when: When.Always يميّز overload القديم (TimeSpan?) عن overload الجديد
        // (Expiration) في StackExchange.Redis ≥ 2.12. بدون when:، التحليل يختار
        // overload الـ Expiration ويفشل النوع.
        return _db.StringSetAsync(key, Serialize(value), expiry: ttl, when: When.Always);
    }

    public Task<bool> RemoveAsync(string key, CancellationToken ct = default) => _db.KeyDeleteAsync(key);
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => _db.KeyExistsAsync(key);

    public Task<long> IncrementAsync(string key, long delta = 1, CancellationToken ct = default)
        => _db.StringIncrementAsync(key, delta);

    public Task<bool> SetIfNotExistsAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken ct = default)
        => _db.StringSetAsync(key, Serialize(value), expiry: ResolveTtl(options), when: When.NotExists);

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CacheEntryOptions? options = null, CancellationToken ct = default)
    {
        var raw = await _db.StringGetAsync(key);
        if (!raw.IsNullOrEmpty) return Deserialize<T>(raw!)!;

        var value = await factory().ConfigureAwait(false);
        await _db.StringSetAsync(key, Serialize(value), expiry: ResolveTtl(options), when: When.Always);
        return value;
    }

    public async Task<IDistributedLock?> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        var token = Guid.NewGuid().ToString("N");
        var ok = await _db.StringSetAsync(key, token, expiry: ttl, when: When.NotExists);
        if (!ok) return null;
        return new RedisLock(_db, key, token, _logger);
    }

    private static TimeSpan? ResolveTtl(CacheEntryOptions? options)
        => options?.AbsoluteExpiration ?? options?.SlidingExpiration;

    // === Serialization ======================================================

    private static RedisValue Serialize<T>(T value)
    {
        if (value is null) return RedisValue.EmptyString;
        if (value is string s) return s;
        if (value is byte[] b) return b;
        return JsonSerializer.Serialize(value);
    }

    private static T? Deserialize<T>(RedisValue raw)
    {
        if (typeof(T) == typeof(string)) return (T)(object)raw.ToString();
        if (typeof(T) == typeof(byte[])) return (T)(object)(byte[])raw!;
        return JsonSerializer.Deserialize<T>((string)raw!);
    }

    private sealed class RedisLock : IDistributedLock
    {
        public string Key { get; }
        private readonly IDatabase _db;
        private readonly string _token;
        private readonly ILogger _logger;
        private bool _released;

        public RedisLock(IDatabase db, string key, string token, ILogger logger)
        {
            _db = db; Key = key; _token = token; _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            if (_released) return;
            _released = true;
            try
            {
                await _db.ScriptEvaluateAsync(ReleaseLockScript, new RedisKey[] { Key }, new RedisValue[] { _token });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RedisCache] failed to release lock {Key}", Key);
            }
        }
    }
}
