using ACommerce.Chat.Operations;
using ACommerce.Kits.Auth.Operations;
using ACommerce.Kits.Chat.Backend;
using ACommerce.Kits.Notifications.Backend;
using ACommerce.Kits.Versions.Backend;
using ACommerce.Kits.Versions.Operations;
using Ejar.Domain;

namespace Ejar.Api.Stores;

/// <summary>
/// جسر بين Auth Kit و EjarSeed على جانب المستأجر (Customer).
/// كامل سطح "ابحث عن الجوال، أنشئ مستخدماً إن لم يوجد، أعد الاسم" → دالّتان.
/// </summary>
public sealed class EjarCustomerAuthUserStore : IAuthUserStore
{
    public Task<string> GetOrCreateUserIdAsync(string phone, CancellationToken ct)
        => Task.FromResult(EjarSeed.GetOrCreateUserId(phone));

    public Task<string?> GetDisplayNameAsync(string userId, CancellationToken ct)
        => Task.FromResult(EjarSeed.GetUser(userId)?.FullName);
}

/// <summary>
/// جسر بين Chat Kit و EjarSeed على جانب المستأجر. في الـ seed كل محادثة بين
/// "me" (المستأجر) و PartnerId (المالك). يرى المستأجر كل محادثاته.
/// مرآة لـ EjarProviderChatStore لكن بمنظور العميل.
/// </summary>
public sealed class EjarCustomerChatStore : IChatStore
{
    public Task<bool> CanParticipateAsync(string conversationId, string userId, CancellationToken ct)
    {
        var c = EjarSeed.Conversations.FirstOrDefault(x => x.Id == conversationId);
        return Task.FromResult(c is not null);
    }

    public Task<IChatMessage> AppendMessageAsync(string conversationId, string senderId, string body, CancellationToken ct)
    {
        var ix = EjarSeed.Conversations.FindIndex(c => c.Id == conversationId);
        if (ix < 0) throw new InvalidOperationException("conversation_not_found");
        var conv = EjarSeed.Conversations[ix];
        var msg  = new EjarSeed.MessageSeed(
            $"M-{conv.Messages.Count + 1}", conversationId, senderId, body, DateTime.UtcNow);
        conv.Messages.Add(msg);
        EjarSeed.Conversations[ix] = conv with { LastAt = msg.SentAt, UnreadCount = 0 };
        return Task.FromResult<IChatMessage>(msg);
    }

    public Task<IReadOnlyList<IChatMessage>> GetMessagesAsync(string conversationId, CancellationToken ct)
    {
        var c = EjarSeed.Conversations.FirstOrDefault(x => x.Id == conversationId);
        IReadOnlyList<IChatMessage> rows = c is null
            ? Array.Empty<IChatMessage>()
            : c.Messages.Cast<IChatMessage>().ToList();
        return Task.FromResult(rows);
    }

    public Task<IChatConversation?> GetConversationAsync(string conversationId, CancellationToken ct)
    {
        var c = EjarSeed.Conversations.FirstOrDefault(x => x.Id == conversationId);
        return Task.FromResult<IChatConversation?>(c);
    }

    public Task<IReadOnlyList<IChatConversation>> ListForUserAsync(string userId, CancellationToken ct)
    {
        IReadOnlyList<IChatConversation> rows = EjarSeed.Conversations
            .Cast<IChatConversation>()
            .ToList();
        return Task.FromResult(rows);
    }
}

/// <summary>
/// مخزن إشعارات المستأجر — يقرأ من EjarSeed (مرآة لـ EjarCustomerChatStore).
/// المستأجر يرى كلّ إشعارات الـ seed (لا تمييز per-user حالياً).
/// </summary>
public sealed class EjarCustomerNotificationStore : INotificationStore
{
    public Task<IReadOnlyList<NotificationItem>> ListAsync(string userId, CancellationToken ct)
    {
        IReadOnlyList<NotificationItem> rows = EjarSeed.Notifications
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationItem(
                n.Id, n.Type, n.Title, n.Body, n.CreatedAt, n.IsRead, n.RelatedId))
            .ToList();
        return Task.FromResult(rows);
    }

    public Task<bool> MarkReadAsync(string userId, string notificationId, CancellationToken ct)
    {
        var ix = EjarSeed.Notifications.FindIndex(n => n.Id == notificationId);
        if (ix < 0) return Task.FromResult(false);
        EjarSeed.Notifications[ix] = EjarSeed.Notifications[ix] with { IsRead = true };
        return Task.FromResult(true);
    }

    public Task<int> MarkAllReadAsync(string userId, CancellationToken ct)
    {
        var count = 0;
        for (var i = 0; i < EjarSeed.Notifications.Count; i++)
        {
            if (EjarSeed.Notifications[i].IsRead) continue;
            EjarSeed.Notifications[i] = EjarSeed.Notifications[i] with { IsRead = true };
            count++;
        }
        return Task.FromResult(count);
    }
}

/// <summary>
/// مخزن إصدارات إيجار — in-memory ابتدائيّ (يستبدله التطبيق بمخزن DB قبل الإنتاج).
/// يجزّئ الإصدارات حسب المنصّة ويحدّد الأحدث ضمن الحالة <see cref="VersionStatus.Latest"/>.
/// </summary>
public sealed class EjarVersionStore : IVersionStore
{
    private static readonly object _lock = new();
    private static readonly List<AppVersion> _rows = new()
    {
        new AppVersion("web",    "1.0.0", VersionStatus.Latest,
            DownloadUrl: "https://ejar.ye/download"),
        new AppVersion("mobile", "1.0.0", VersionStatus.Latest,
            DownloadUrl: "https://ejar.ye/download/mobile"),
        new AppVersion("admin",  "1.0.0", VersionStatus.Latest,
            DownloadUrl: "https://ejar.ye/download/admin"),
    };

    public Task<IReadOnlyList<AppVersion>> ListAsync(string? platform, CancellationToken ct)
    {
        lock (_lock)
        {
            IReadOnlyList<AppVersion> rows = string.IsNullOrEmpty(platform)
                ? _rows.ToList()
                : _rows.Where(r => r.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase)).ToList();
            return Task.FromResult(rows);
        }
    }

    public Task<AppVersion?> GetAsync(string platform, string version, CancellationToken ct)
    {
        lock (_lock)
        {
            var row = _rows.FirstOrDefault(r =>
                r.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase) &&
                r.Version.Equals(version,   StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<AppVersion?>(row);
        }
    }

    public Task<AppVersion?> GetLatestAsync(string platform, CancellationToken ct)
    {
        lock (_lock)
        {
            var row = _rows
                .Where(r => r.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase) &&
                            r.Status == VersionStatus.Latest)
                .OrderByDescending(r => r.Version, StringComparer.Ordinal)
                .FirstOrDefault();
            return Task.FromResult<AppVersion?>(row);
        }
    }

    public Task<AppVersion> UpsertAsync(AppVersion version, CancellationToken ct)
    {
        lock (_lock)
        {
            var ix = _rows.FindIndex(r =>
                r.Platform.Equals(version.Platform, StringComparison.OrdinalIgnoreCase) &&
                r.Version.Equals(version.Version,   StringComparison.OrdinalIgnoreCase));
            if (ix >= 0) _rows[ix] = version;
            else _rows.Add(version);
            return Task.FromResult(version);
        }
    }

    public Task<bool> SetStatusAsync(
        string platform, string version, VersionStatus status, DateTime? sunsetAt, CancellationToken ct)
    {
        lock (_lock)
        {
            var ix = _rows.FindIndex(r =>
                r.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase) &&
                r.Version.Equals(version,   StringComparison.OrdinalIgnoreCase));
            if (ix < 0) return Task.FromResult(false);
            _rows[ix] = _rows[ix] with { Status = status, SunsetAt = sunsetAt };
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteAsync(string platform, string version, CancellationToken ct)
    {
        lock (_lock)
        {
            var n = _rows.RemoveAll(r =>
                r.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase) &&
                r.Version.Equals(version,   StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(n > 0);
        }
    }
}
