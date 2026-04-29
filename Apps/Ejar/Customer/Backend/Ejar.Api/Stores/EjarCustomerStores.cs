using ACommerce.Chat.Operations;
using ACommerce.Kits.Auth.Operations;
using ACommerce.Kits.Chat.Backend;
using ACommerce.Kits.Notifications.Backend;
using ACommerce.Kits.Versions.Backend;
using ACommerce.Kits.Versions.Operations;
using Ejar.Api.Data;
using Ejar.Domain;
using Microsoft.EntityFrameworkCore;

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
/// مخزن إصدارات إيجار — مدعوم بـ EF Core. يقرأ ويكتب جدول <c>AppVersions</c>
/// ويُسلِّم العقد <see cref="AppVersion"/> الذي يفهمه Versions Kit.
/// المسؤول عن الإدارة الإداريّة (إضافة/تحديث الحالة/حذف) عبر
/// <c>AdminVersionsController</c> الذي يستدعي هذا الـ store.
/// </summary>
public sealed class EjarVersionStore : IVersionStore
{
    private readonly EjarDbContext _db;
    public EjarVersionStore(EjarDbContext db) => _db = db;

    public async Task<IReadOnlyList<AppVersion>> ListAsync(string? platform, CancellationToken ct)
    {
        var q = _db.AppVersions.AsNoTracking();
        if (!string.IsNullOrEmpty(platform))
            q = q.Where(v => v.Platform == platform);
        var rows = await q.ToListAsync(ct);
        return rows.Select(ToContract).ToList();
    }

    public async Task<AppVersion?> GetAsync(string platform, string version, CancellationToken ct)
    {
        var row = await _db.AppVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Platform == platform && v.Version == version, ct);
        return row is null ? null : ToContract(row);
    }

    public async Task<AppVersion?> GetLatestAsync(string platform, CancellationToken ct)
    {
        var latestStatus = (int)VersionStatus.Latest;
        var row = await _db.AppVersions.AsNoTracking()
            .Where(v => v.Platform == platform && v.Status == latestStatus)
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync(ct);
        return row is null ? null : ToContract(row);
    }

    public async Task<AppVersion> UpsertAsync(AppVersion version, CancellationToken ct)
    {
        var existing = await _db.AppVersions
            .FirstOrDefaultAsync(v => v.Platform == version.Platform && v.Version == version.Version, ct);
        if (existing is null)
        {
            _db.AppVersions.Add(new AppVersionEntity {
                Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
                Platform = version.Platform, Version = version.Version,
                Status = (int)version.Status, SunsetAt = version.SunsetAt,
                Notes  = version.Notes,      DownloadUrl = version.DownloadUrl,
            });
        }
        else
        {
            existing.Status      = (int)version.Status;
            existing.SunsetAt    = version.SunsetAt;
            existing.Notes       = version.Notes;
            existing.DownloadUrl = version.DownloadUrl;
            existing.UpdatedAt   = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return version;
    }

    public async Task<bool> SetStatusAsync(
        string platform, string version, VersionStatus status, DateTime? sunsetAt, CancellationToken ct)
    {
        var row = await _db.AppVersions
            .FirstOrDefaultAsync(v => v.Platform == platform && v.Version == version, ct);
        if (row is null) return false;
        row.Status    = (int)status;
        row.SunsetAt  = sunsetAt;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(string platform, string version, CancellationToken ct)
    {
        var row = await _db.AppVersions
            .FirstOrDefaultAsync(v => v.Platform == platform && v.Version == version, ct);
        if (row is null) return false;
        row.IsDeleted = true;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static AppVersion ToContract(AppVersionEntity e) =>
        new(e.Platform, e.Version, (VersionStatus)e.Status, e.SunsetAt, e.Notes, e.DownloadUrl);
}
