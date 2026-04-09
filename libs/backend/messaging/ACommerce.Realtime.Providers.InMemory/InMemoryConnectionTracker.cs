using ACommerce.Realtime.Operations.Abstractions;
using System.Collections.Concurrent;

namespace ACommerce.Realtime.Providers.InMemory;

/// <summary>
/// تتبع الاتصالات في الذاكرة - للتطوير والاختبار.
/// </summary>
public class InMemoryConnectionTracker : IConnectionTracker
{
    private readonly ConcurrentDictionary<string, string> _connections = new(StringComparer.OrdinalIgnoreCase);

    public Task TrackConnectionAsync(string userId, string connectionId, CancellationToken ct = default)
    {
        _connections[userId] = connectionId;
        return Task.CompletedTask;
    }

    public Task RemoveConnectionAsync(string userId, CancellationToken ct = default)
    {
        _connections.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    public Task<string?> GetConnectionIdAsync(string userId, CancellationToken ct = default)
    {
        _connections.TryGetValue(userId, out var connectionId);
        return Task.FromResult(connectionId);
    }

    public Task<bool> IsOnlineAsync(string userId, CancellationToken ct = default)
        => Task.FromResult(_connections.ContainsKey(userId));

    public Task<Dictionary<string, bool>> GetPresenceAsync(
        IEnumerable<string> userIds,
        CancellationToken ct = default)
    {
        var result = userIds.ToDictionary(
            uid => uid,
            uid => _connections.ContainsKey(uid),
            StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(result);
    }

    /// <summary>جميع المستخدمين المتصلين حالياً</summary>
    public IReadOnlyDictionary<string, string> ActiveConnections => _connections;

    public int OnlineCount => _connections.Count;

    public void Clear() => _connections.Clear();
}
