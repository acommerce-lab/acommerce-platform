namespace ACommerce.Realtime.Operations.Abstractions;

/// <summary>
/// واجهة النقل في الزمن الحقيقي.
/// لا SignalR. لا WebSocket. مجرد عقد.
/// المزود (SignalR, gRPC, etc.) يُطبق هذه الواجهة في طبقة التطبيق.
/// </summary>
public interface IRealtimeTransport
{
    Task SendToUserAsync(string userId, string method, object data, CancellationToken ct = default);
    Task SendToGroupAsync(string groupName, string method, object data, CancellationToken ct = default);
    Task BroadcastAsync(string method, object data, CancellationToken ct = default);
    Task AddToGroupAsync(string connectionId, string groupName, CancellationToken ct = default);
    Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken ct = default);
}

/// <summary>
/// واجهة تتبع الاتصالات. المطور يقرر: InMemory? Redis? DB?
/// </summary>
public interface IConnectionTracker
{
    Task TrackConnectionAsync(string userId, string connectionId, CancellationToken ct = default);
    Task RemoveConnectionAsync(string userId, CancellationToken ct = default);
    Task<string?> GetConnectionIdAsync(string userId, CancellationToken ct = default);
    Task<bool> IsOnlineAsync(string userId, CancellationToken ct = default);
    Task<Dictionary<string, bool>> GetPresenceAsync(IEnumerable<string> userIds, CancellationToken ct = default);
}
