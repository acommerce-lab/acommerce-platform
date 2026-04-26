using System.Collections.Concurrent;
using ACommerce.Cache.Operations.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace ACommerce.Cache.Providers.InMemory;

/// <summary>
/// تطبيق <see cref="ICache"/> داخل العمليّة. مبنيّ فوق
/// <see cref="IMemoryCache"/> لإدارة المهلات وLRU، مع <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// للأقفال الموزَّعة (التي ليست موزَّعة فعلاً هنا — تعمل ضمن العمليّة فقط).
/// </summary>
public sealed class InMemoryCache : ICache
{
    private readonly IMemoryCache _mem;
    private readonly ConcurrentDictionary<string, byte> _locks = new();

    public InMemoryCache(IMemoryCache mem) { _mem = mem; }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (_mem.TryGetValue<Entry>(key, out var entry) && entry is not null)
        {
            entry.Touch(); // sliding refresh
            return Task.FromResult((T?)entry.Value);
        }
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken ct = default)
    {
        var entry = new Entry(value, options?.SlidingExpiration);
        var memOpts = ToMemOptions(options, entry);
        _mem.Set(key, entry, memOpts);
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(string key, CancellationToken ct = default)
    {
        var existed = _mem.TryGetValue(key, out _);
        _mem.Remove(key);
        return Task.FromResult(existed);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_mem.TryGetValue(key, out _));

    public Task<long> IncrementAsync(string key, long delta = 1, CancellationToken ct = default)
    {
        // Atomic via lock on the memory cache entry — ConcurrentDictionary not used because
        // IMemoryCache also handles eviction.
        lock (_mem)
        {
            var current = _mem.TryGetValue<Entry>(key, out var e) && e?.Value is long l ? l : 0L;
            var next = current + delta;
            _mem.Set(key, new Entry(next, null));
            return Task.FromResult(next);
        }
    }

    public Task<bool> SetIfNotExistsAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken ct = default)
    {
        lock (_mem)
        {
            if (_mem.TryGetValue(key, out _)) return Task.FromResult(false);
            var entry = new Entry(value, options?.SlidingExpiration);
            _mem.Set(key, entry, ToMemOptions(options, entry));
            return Task.FromResult(true);
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CacheEntryOptions? options = null, CancellationToken ct = default)
    {
        if (_mem.TryGetValue<Entry>(key, out var entry) && entry is not null)
        {
            entry.Touch();
            return (T)entry.Value!;
        }
        var value = await factory().ConfigureAwait(false);
        await SetAsync(key, value, options, ct);
        return value;
    }

    public Task<IDistributedLock?> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        // Local-only "distributed" lock — useful for dev only.
        if (!_locks.TryAdd(key, 0)) return Task.FromResult<IDistributedLock?>(null);
        // Auto-release after TTL.
        _ = Task.Delay(ttl, ct).ContinueWith(_ => _locks.TryRemove(key, out _), TaskScheduler.Default);
        return Task.FromResult<IDistributedLock?>(new Handle(key, _locks));
    }

    private static MemoryCacheEntryOptions ToMemOptions(CacheEntryOptions? options, Entry entry)
    {
        var memOpts = new MemoryCacheEntryOptions();
        if (options?.AbsoluteExpiration is { } abs) memOpts.AbsoluteExpirationRelativeToNow = abs;
        if (options?.SlidingExpiration is { } slide) memOpts.SlidingExpiration = slide;
        return memOpts;
    }

    private sealed class Entry
    {
        public object? Value { get; }
        public TimeSpan? Sliding { get; }
        public Entry(object? value, TimeSpan? sliding) { Value = value; Sliding = sliding; }
        public void Touch() { /* IMemoryCache handles sliding expiration on access */ }
    }

    private sealed class Handle : IDistributedLock
    {
        public string Key { get; }
        private readonly ConcurrentDictionary<string, byte> _set;
        public Handle(string key, ConcurrentDictionary<string, byte> set) { Key = key; _set = set; }
        public ValueTask DisposeAsync() { _set.TryRemove(Key, out _); return ValueTask.CompletedTask; }
    }
}
