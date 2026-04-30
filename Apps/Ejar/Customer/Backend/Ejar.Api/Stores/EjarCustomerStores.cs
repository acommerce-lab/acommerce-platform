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
/// جسر بين Auth Kit وقاعدة البيانات الفعليّة. كامل سطح "ابحث عن الجوال،
/// أنشئ مستخدماً إن لم يوجد، أعد الاسم" → دالّتان مدعومتان بـ EF Core.
///
/// <para>السابق كان يستخدم EjarSeed.GetOrCreateUserId الذي يُرجع <c>"U-1"</c>
/// كنصّ بدل Guid. العميل يستهلكه عبر <c>Guid.TryParse</c> فيفشل ويبقى
/// <c>Store.Auth.UserId = null</c> فيحسبه التطبيق غير مصادَق رغم وجود التوكن
/// — حلقة redirect لا نهائيّة بين /favorites و /login.</para>
/// </summary>
public sealed class EjarCustomerAuthUserStore : IAuthUserStore
{
    private readonly EjarDbContext _db;
    public EjarCustomerAuthUserStore(EjarDbContext db) => _db = db;

    public async Task<string> GetOrCreateUserIdAsync(string phone, CancellationToken ct)
    {
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Phone == phone, ct);
        if (existing is not null) return existing.Id.ToString();

        var newUser = new UserEntity
        {
            Id            = Guid.NewGuid(),
            CreatedAt     = DateTime.UtcNow,
            FullName      = "مستخدم جديد",
            Phone         = phone,
            PhoneVerified = true,
            Email         = "",
            EmailVerified = false,
            City          = "صنعاء",
            MemberSince   = DateTime.UtcNow,
        };
        _db.Users.Add(newUser);
        await _db.SaveChangesAsync(ct);
        return newUser.Id.ToString();
    }

    public async Task<string?> GetDisplayNameAsync(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var id)) return null;
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return u?.FullName;
    }
}

/// <summary>
/// جسر بين Chat Kit و EF. كانت النسخة السابقة تقرأ من <c>EjarSeed.Conversations</c>
/// (قائمة بذور in-memory)، بينما <c>POST /conversations/start</c> في
/// CatalogController يكتب في <c>_db.Conversations</c>. النتيجة: كلّ محادثة
/// يُنشئها المستخدم لا يجدها هذا المخزن، فـ <c>CanParticipateAsync</c> يردّ
/// false و <c>POST /chat/{id}/enter</c> يردّ <b>403 not_a_participant</b>.
/// كلّ الرسائل تفشل بـ "No active conversation".
///
/// <para>هذا التطبيق يقرأ من <see cref="EjarDbContext"/> مباشرةً ويُسلّم
/// adaptors تفي بـ <see cref="IChatConversation"/> و <see cref="IChatMessage"/>.
/// المشاركون = OwnerId و PartnerId (الـ Customer Kit يستعمل بادئة "User:"
/// كـ PartyKind في <c>ChatKitOptions</c>، نطابقها هنا).</para>
/// </summary>
public sealed class EjarCustomerChatStore : IChatStore
{
    private readonly EjarDbContext _db;
    public EjarCustomerChatStore(EjarDbContext db) => _db = db;

    public async Task<bool> CanParticipateAsync(string conversationId, string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(conversationId, out var cid)) return false;
        if (!Guid.TryParse(userId, out var uid))         return false;
        return await _db.Conversations.AsNoTracking()
            .AnyAsync(c => c.Id == cid && (c.OwnerId == uid || c.PartnerId == uid), ct);
    }

    public async Task<IChatMessage> AppendMessageAsync(
        string conversationId, string senderId, string body, CancellationToken ct)
    {
        if (!Guid.TryParse(conversationId, out var cid))
            throw new InvalidOperationException("invalid_conversation_id");

        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == cid, ct);
        if (conv is null) throw new InvalidOperationException("conversation_not_found");

        var msg = new MessageEntity
        {
            Id             = Guid.NewGuid(),
            CreatedAt      = DateTime.UtcNow,
            ConversationId = cid,
            From           = senderId,
            Text           = body,
            SentAt         = DateTime.UtcNow,
        };
        _db.Messages.Add(msg);
        conv.LastAt = msg.SentAt;
        conv.UnreadCount += 1;
        await _db.SaveChangesAsync(ct);

        return new MessageView(msg);
    }

    public async Task<IReadOnlyList<IChatMessage>> GetMessagesAsync(
        string conversationId, CancellationToken ct)
    {
        if (!Guid.TryParse(conversationId, out var cid))
            return Array.Empty<IChatMessage>();
        var rows = await _db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == cid)
            .OrderBy(m => m.SentAt)
            .ToListAsync(ct);
        return rows.Select(m => (IChatMessage)new MessageView(m)).ToList();
    }

    public async Task<IChatConversation?> GetConversationAsync(
        string conversationId, CancellationToken ct)
    {
        if (!Guid.TryParse(conversationId, out var cid)) return null;
        var c = await _db.Conversations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cid, ct);
        if (c is null) return null;
        return await BuildViewAsync(c, ct);
    }

    public async Task<IReadOnlyList<IChatConversation>> ListForUserAsync(
        string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var uid)) return Array.Empty<IChatConversation>();
        var rows = await _db.Conversations.AsNoTracking()
            .Where(c => c.OwnerId == uid || c.PartnerId == uid)
            .OrderByDescending(c => c.LastAt)
            .ToListAsync(ct);
        if (rows.Count == 0) return Array.Empty<IChatConversation>();

        // ابحث عن أسماء الـ Owner و Partner في طلب واحد بدل n+1.
        var partyIds = rows.Select(c => c.OwnerId).Concat(rows.Select(c => c.PartnerId))
                           .Distinct().ToList();
        var users = await _db.Users.AsNoTracking()
            .Where(u => partyIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        // آخر رسالة في كلّ محادثة (للـ inbox preview).
        var convIds = rows.Select(c => c.Id).ToList();
        var lastMsgs = await _db.Messages.AsNoTracking()
            .Where(m => convIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => g.OrderByDescending(m => m.SentAt).First())
            .ToDictionaryAsync(m => m.ConversationId, m => m.Text, ct);

        return rows.Select(c => (IChatConversation)new ConversationView(
            c,
            users.TryGetValue(c.OwnerId,   out var on) ? on : null,
            users.TryGetValue(c.PartnerId, out var pn) ? pn : c.PartnerName,
            lastMsgs.TryGetValue(c.Id, out var last) ? last : null
        )).ToList();
    }

    private async Task<ConversationView> BuildViewAsync(ConversationEntity c, CancellationToken ct)
    {
        var owner   = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == c.OwnerId, ct);
        var partner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == c.PartnerId, ct);
        return new ConversationView(c, owner?.FullName, partner?.FullName ?? c.PartnerName, lastMessage: null);
    }

    // ── views ──────────────────────────────────────────────────────────────
    // PartyKind بادئة "User:" تطابق ChatKitOptions في AddChatKit؛ بدونها
    // BroadcastNewMessageAsync يبثّ إلى partyId خاطئ والمستلم لا يستلم شيء.
    //
    // الـ ConversationView يحوي حقولاً عامّة (OwnerId/OwnerName/PartnerId/
    // PartnerName/Subject/ListingId/LastAt/UnreadCount/LastMessage) إضافةً
    // للحقول من IChatConversation. JSON يُصدّر كل الخصائص العامّة من النوع
    // الفعليّ، فالواجهة الأماميّة تستلمها كلّها وتختار "الطرف الآخر" حسب
    // userId الحاليّ.
    private sealed class MessageView : IChatMessage
    {
        private readonly MessageEntity _e;
        public MessageView(MessageEntity e) => _e = e;
        public string    Id             => _e.Id.ToString();
        public string    ConversationId => _e.ConversationId.ToString();
        public string    SenderPartyId  => $"User:{_e.From}";
        public string    Body           => _e.Text;
        public DateTime  SentAt         => _e.SentAt;
        public DateTime? ReadAt         => null;
    }

    private sealed class ConversationView : IChatConversation
    {
        private readonly ConversationEntity _e;
        public ConversationView(ConversationEntity e, string? ownerName, string? partnerName, string? lastMessage)
        {
            _e = e;
            OwnerName   = ownerName ?? "—";
            PartnerName = partnerName ?? "—";
            LastMessage = lastMessage;
        }
        public string Id => _e.Id.ToString();
        public IReadOnlyList<string> ParticipantPartyIds => new[]
        {
            $"User:{_e.OwnerId}", $"User:{_e.PartnerId}"
        };

        public string OwnerId     => _e.OwnerId.ToString();
        public string OwnerName   { get; }
        public string PartnerId   => _e.PartnerId.ToString();
        public string PartnerName { get; }
        public string Subject     => _e.Subject;
        public string ListingId   => _e.ListingId.ToString();
        public DateTime LastAt    => _e.LastAt;
        public int UnreadCount    => _e.UnreadCount;
        public string? LastMessage { get; }
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
        // قاعدة الكيت: حالة Latest فريدة لكلّ منصّة. عند Upsert لإصدار جديد
        // كـ Latest نُخفّض أيّ "Latest" سابق في نفس المنصّة إلى Active تلقائياً
        // — حتى لا يبقى DB بإصدارَين Latest فيختار GetLatestAsync أحدهما عشوائياً
        // ويظهر للعميل بانر "هناك أحدث: 1.0.0" بعد أن نشرنا 2026.04.29.1 فعلاً.
        if (version.Status == VersionStatus.Latest)
        {
            var priorLatestStatus = (int)VersionStatus.Latest;
            var activeStatus      = (int)VersionStatus.Active;
            var priors = await _db.AppVersions
                .Where(v => v.Platform == version.Platform
                         && v.Status   == priorLatestStatus
                         && v.Version  != version.Version)
                .ToListAsync(ct);
            foreach (var p in priors)
            {
                p.Status    = activeStatus;
                p.UpdatedAt = DateTime.UtcNow;
            }
        }

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

        // نفس قاعدة UpsertAsync — لو رفعنا هذا الصفّ إلى Latest نُخفِّض السابق.
        if (status == VersionStatus.Latest)
        {
            var latestStatus = (int)VersionStatus.Latest;
            var activeStatus = (int)VersionStatus.Active;
            var priors = await _db.AppVersions
                .Where(v => v.Platform == platform
                         && v.Status   == latestStatus
                         && v.Version  != version)
                .ToListAsync(ct);
            foreach (var p in priors) { p.Status = activeStatus; p.UpdatedAt = DateTime.UtcNow; }
        }

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
