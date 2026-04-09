using ACommerce.Realtime.Operations.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ACommerce.Realtime.Providers.InMemory;

/// <summary>
/// نقل زمن حقيقي في الذاكرة - للتطوير والاختبار.
///
/// يحتفظ بسجل كل الرسائل المُرسلة ويتيح التحقق منها في الاختبارات.
/// لا يرسل شيئاً عبر الشبكة - كل شيء داخل العملية.
/// </summary>
public class InMemoryRealtimeTransport : IRealtimeTransport
{
    private readonly ILogger<InMemoryRealtimeTransport> _logger;
    private readonly ConcurrentQueue<SentMessage> _messageLog = new();

    // userId → registered handlers (للاختبارات)
    private readonly ConcurrentDictionary<string, List<Func<string, object, Task>>> _userHandlers = new();
    private readonly ConcurrentDictionary<string, List<Func<string, object, Task>>> _groupHandlers = new();
    private readonly List<Func<string, object, Task>> _broadcastHandlers = new();

    // connectionId → groups
    private readonly ConcurrentDictionary<string, HashSet<string>> _connectionGroups = new();

    public InMemoryRealtimeTransport(ILogger<InMemoryRealtimeTransport> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendToUserAsync(string userId, string method, object data, CancellationToken ct = default)
    {
        _logger.LogDebug("[InMemory] → User:{UserId} | {Method}", userId, method);
        _messageLog.Enqueue(new SentMessage(SentMessageTarget.User, userId, method, data));

        if (_userHandlers.TryGetValue(userId, out var handlers))
        {
            foreach (var h in handlers)
                await h(method, data);
        }
    }

    public async Task SendToGroupAsync(string groupName, string method, object data, CancellationToken ct = default)
    {
        _logger.LogDebug("[InMemory] → Group:{Group} | {Method}", groupName, method);
        _messageLog.Enqueue(new SentMessage(SentMessageTarget.Group, groupName, method, data));

        if (_groupHandlers.TryGetValue(groupName, out var handlers))
        {
            foreach (var h in handlers)
                await h(method, data);
        }
    }

    public async Task BroadcastAsync(string method, object data, CancellationToken ct = default)
    {
        _logger.LogDebug("[InMemory] → Broadcast | {Method}", method);
        _messageLog.Enqueue(new SentMessage(SentMessageTarget.All, "*", method, data));

        foreach (var h in _broadcastHandlers)
            await h(method, data);
    }

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
    {
        _connectionGroups.AddOrUpdate(
            connectionId,
            _ => new HashSet<string> { groupName },
            (_, set) => { set.Add(groupName); return set; });
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
    {
        if (_connectionGroups.TryGetValue(connectionId, out var groups))
            groups.Remove(groupName);
        return Task.CompletedTask;
    }

    // =============================================
    // Helpers للاختبار
    // =============================================

    /// <summary>جميع الرسائل المُرسلة منذ آخر Clear</summary>
    public IReadOnlyList<SentMessage> SentMessages => _messageLog.ToArray();

    /// <summary>الرسائل المُرسلة لمستخدم محدد</summary>
    public IEnumerable<SentMessage> GetUserMessages(string userId)
        => _messageLog.Where(m => m.Target == SentMessageTarget.User && m.Recipient == userId);

    /// <summary>الرسائل المُرسلة لمجموعة محددة</summary>
    public IEnumerable<SentMessage> GetGroupMessages(string groupName)
        => _messageLog.Where(m => m.Target == SentMessageTarget.Group && m.Recipient == groupName);

    /// <summary>مسح السجل</summary>
    public void Clear()
    {
        while (_messageLog.TryDequeue(out _)) { }
    }

    /// <summary>تسجيل handler لمستخدم (للاختبار)</summary>
    public void OnUserMessage(string userId, Func<string, object, Task> handler)
    {
        _userHandlers.AddOrUpdate(userId,
            _ => new List<Func<string, object, Task>> { handler },
            (_, list) => { list.Add(handler); return list; });
    }

    /// <summary>تسجيل handler لمجموعة (للاختبار)</summary>
    public void OnGroupMessage(string groupName, Func<string, object, Task> handler)
    {
        _groupHandlers.AddOrUpdate(groupName,
            _ => new List<Func<string, object, Task>> { handler },
            (_, list) => { list.Add(handler); return list; });
    }
}

/// <summary>رسالة مُسجلة في الذاكرة</summary>
public record SentMessage(
    SentMessageTarget Target,
    string Recipient,
    string Method,
    object Data)
{
    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum SentMessageTarget { User, Group, All }
