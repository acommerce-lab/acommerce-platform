namespace ACommerce.Notification.Providers.Firebase.Storage;

/// <summary>
/// مخزن رموز الأجهزة. لا كيان - مجرد عقد.
/// المطور يُطبقه (DB, Redis, InMemory...).
/// </summary>
public interface IDeviceTokenStore
{
    /// <summary>إضافة رمز جهاز لمستخدم</summary>
    Task RegisterAsync(string userId, string deviceToken, string? platform = null, CancellationToken ct = default);

    /// <summary>إزالة رمز جهاز</summary>
    Task UnregisterAsync(string deviceToken, CancellationToken ct = default);

    /// <summary>الحصول على جميع رموز أجهزة مستخدم (قد يكون لديه عدة أجهزة)</summary>
    Task<IReadOnlyList<string>> GetTokensAsync(string userId, CancellationToken ct = default);

    /// <summary>إزالة كل رموز مستخدم</summary>
    Task RemoveAllForUserAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// تطبيق InMemory افتراضي للتطوير والاختبار.
/// </summary>
public class InMemoryDeviceTokenStore : IDeviceTokenStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, HashSet<string>> _userTokens =
        new(StringComparer.OrdinalIgnoreCase);

    public Task RegisterAsync(string userId, string deviceToken, string? platform = null, CancellationToken ct = default)
    {
        _userTokens.AddOrUpdate(
            userId,
            _ => new HashSet<string> { deviceToken },
            (_, set) => { lock (set) { set.Add(deviceToken); } return set; });
        return Task.CompletedTask;
    }

    public Task UnregisterAsync(string deviceToken, CancellationToken ct = default)
    {
        foreach (var set in _userTokens.Values)
        {
            lock (set) { set.Remove(deviceToken); }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetTokensAsync(string userId, CancellationToken ct = default)
    {
        if (!_userTokens.TryGetValue(userId, out var set))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        lock (set)
        {
            return Task.FromResult<IReadOnlyList<string>>(set.ToArray());
        }
    }

    public Task RemoveAllForUserAsync(string userId, CancellationToken ct = default)
    {
        _userTokens.TryRemove(userId, out _);
        return Task.CompletedTask;
    }
}
