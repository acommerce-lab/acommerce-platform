using ACommerce.Chat.Operations;
using ACommerce.Kits.Auth.Operations;
using ACommerce.Kits.Chat.Backend;
using ACommerce.Kits.Notifications.Backend;
using ACommerce.Kits.Versions.Backend;
using ACommerce.Kits.Versions.Operations;
// Phase F3: لا حاجة لـ INotificationChannel أو IRealtimeTransport هنا —
// السلوكان يعيشان في compositions خارجيّة.
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
            City          = "إب",
            MemberSince   = DateTime.UtcNow,
        };
        _db.Users.Add(newUser);
        // (F6) لا SaveChangesAsync — AuthController.VerifyOtp يضع .SaveAtEnd().
        // الذرّيّة هنا تُهمّ لو سُجِّل interceptor للـ audit يضيف صفّاً عند
        // تسجيل دخول جديد: المستخدم + الـ audit يُحفظان معاً، لا أحدهما بدون الآخر.
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
    // Phase F3: الـ store الآن نقيّ — لا realtime، لا notifications، لا FCM.
    // كلّ هذه side effects تأتي من compositions خارجيّة (Chat.Realtime،
    // Chat.WithNotifications) عبر interceptors على message.send.
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
        // مسار قديم — يبقى للـ Support kit وغيره ممّن لم يُرحَّل بعد.
        // يبني الرسالة كـ POCO ثمّ يُمرّرها لـ AppendNoSaveAsync (المسار
        // الجديد) ليتمحور كل المسار حول IChatMessage بدل إنشاء MessageEntity
        // مباشرةً.
        var msg = new InMemoryChatMessage(
            Id:             Guid.NewGuid().ToString(),
            ConversationId: conversationId,
            SenderPartyId:  $"User:{senderId}",
            Body:           body,
            SentAt:         DateTime.UtcNow);
        await AppendNoSaveAsync(msg, ct);
        return msg;
    }

    public async Task AppendNoSaveAsync(IChatMessage message, CancellationToken ct)
    {
        if (!Guid.TryParse(message.ConversationId, out var cid))
            throw new InvalidOperationException("invalid_conversation_id");

        // ابحث في الـ ChangeTracker المحلّيّ أوّلاً — في عمليّات مركّبة
        // (Support.Open ينشئ Conversation + Ticket + الرسالة الأولى داخل
        // نفس الـ scope) الـ Conversation تكون tracked لكنّها لم تُحفظ بعد،
        // فاستعلام DB يردّ null. Local يكشفها مباشرةً.
        var conv = _db.Conversations.Local.FirstOrDefault(c => c.Id == cid)
                ?? await _db.Conversations.FirstOrDefaultAsync(c => c.Id == cid, ct);
        if (conv is null)
        {
            // لا محادثة → لا persistence. الرسالة تبقى حدثاً OAM صالحاً
            // (نزل لـ Post-interceptors)، لكن لا صفّ في DB. الـ broadcast
            // ينطلق على ConversationId المُعطى — التطبيق يقرّر هل ذلك
            // مقبول (مثلاً جلسة عابرة) أو لا (يرفض في analyzer مسبقاً).
            return;
        }

        // SenderPartyId قد يأتي بصيغة "User:GUID" (من ChatController) أو
        // GUID خام (من المسار القديم AppendMessageAsync). نقبل الاثنين.
        var senderRaw = message.SenderPartyId;
        var idx = senderRaw.IndexOf(':');
        var senderId = idx >= 0 ? senderRaw[(idx + 1)..] : senderRaw;

        var msgId = Guid.TryParse(message.Id, out var mid) ? mid : Guid.NewGuid();
        var entity = new MessageEntity
        {
            Id             = msgId,
            CreatedAt      = message.SentAt,
            ConversationId = cid,
            From           = senderId,
            Text           = message.Body,
            SentAt         = message.SentAt,
        };
        _db.Messages.Add(entity);          // tracked
        conv.LastAt      = message.SentAt;
        conv.UnreadCount += 1;
        // (F6) لا SaveChanges — ChatController.Send يضع .SaveAtEnd().
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
/// مخزن إشعارات إيجار — مدعوم بـ EF (جدول <c>Notifications</c> per-user).
/// كان السابق يقرأ من <c>EjarSeed.Notifications</c> (قائمة بذور in-memory
/// مشتركة بين كل المستخدمين)، فيرى كل مستخدم نفس البذرة بدون أيّ ربط بحسابه
/// الفعليّ ولا تُحفَظ الإشعارات الجديدة في DB. الآن:
/// <list type="bullet">
///   <item><see cref="ListAsync"/> ↦ <c>WHERE UserId = @uid</c>.</item>
///   <item><see cref="MarkReadAsync"/> / <see cref="MarkAllReadAsync"/> ↦ تحديثات على الصفّ الصحيح.</item>
///   <item>إنشاء الإشعار يحدث في <c>EjarCustomerChatStore.AppendMessageAsync</c>
///         عند كل رسالة جديدة (مرفقة بـ realtime broadcast على نفس الـ
///         conversation channel).</item>
/// </list>
/// </summary>
public sealed class EjarCustomerNotificationStore : INotificationStore
{
    private readonly EjarDbContext _db;
    public EjarCustomerNotificationStore(EjarDbContext db) => _db = db;

    public async Task<IReadOnlyList<NotificationItem>> ListAsync(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var uid)) return Array.Empty<NotificationItem>();
        var rows = await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == uid)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(n => new NotificationItem(
            n.Id.ToString(), n.Type, n.Title, n.Body, n.CreatedAt, n.IsRead, n.RelatedId)).ToList();
    }

    public async Task<bool> MarkReadAsync(string userId, string notificationId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var uid))     return false;
        if (!Guid.TryParse(notificationId, out var nid)) return false;
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == nid && x.UserId == uid, ct);
        if (n is null) return false;
        n.IsRead = true; n.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> MarkAllReadAsync(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var uid)) return 0;
        var rows = await _db.Notifications
            .Where(n => n.UserId == uid && !n.IsRead).ToListAsync(ct);
        foreach (var n in rows) { n.IsRead = true; n.UpdatedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    public async Task<NotificationItem> CreateAsync(string userId, string type, string title,
        string body, string? relatedId = null, CancellationToken ct = default)
    {
        // مسار قديم — يحفظ مباشرةً. متروك للتوافق مع أيّ متّصل خارجيّ
        // غير مرحَّل لـ INotificationDispatcher بعد. المسار المفضّل الآن
        // يمرّ بـ AddNoSaveAsync داخل OAM envelope مع SaveAtEnd.
        var item = await AddNoSaveAsync(userId, type, title, body, relatedId, ct);
        await _db.SaveChangesAsync(ct);
        return item;
    }

    public Task<NotificationItem> AddNoSaveAsync(string userId, string type, string title,
        string body, string? relatedId = null, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var uid))
            throw new InvalidOperationException("invalid_user_id");
        var entity = new NotificationEntity
        {
            Id        = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UserId    = uid,
            Title     = title.Length > 200 ? title[..200] : title,
            Body      = body,
            Type      = type.Length > 40 ? type[..40] : type,
            RelatedId = relatedId is { Length: > 64 } ? relatedId[..64] : relatedId,
            IsRead    = false,
        };
        _db.Notifications.Add(entity);
        // (F6) لا SaveChangesAsync — INotificationDispatcher يضع .SaveAtEnd().
        return Task.FromResult(new NotificationItem(
            entity.Id.ToString(), entity.Type, entity.Title, entity.Body,
            entity.CreatedAt, entity.IsRead, entity.RelatedId));
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
        // (F6) لا SaveChangesAsync — AdminVersionsController.Upsert يضع .SaveAtEnd().
        // الذرّيّة هنا حرجة: demote-prior-Latest + insert/update الجديد يجب أن
        // يحدثا في معاملة واحدة وإلّا قد تظهر فترة فيها إصداران Latest.
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
        // (F6) لا SaveChangesAsync — AdminVersionsController.SetStatus يضع .SaveAtEnd().
        return true;
    }

    public async Task<bool> DeleteAsync(string platform, string version, CancellationToken ct)
    {
        var row = await _db.AppVersions
            .FirstOrDefaultAsync(v => v.Platform == platform && v.Version == version, ct);
        if (row is null) return false;
        row.IsDeleted = true;
        row.UpdatedAt = DateTime.UtcNow;
        // (F6) لا SaveChangesAsync — AdminVersionsController.Delete يضع .SaveAtEnd().
        return true;
    }

    private static AppVersion ToContract(AppVersionEntity e) =>
        new(e.Platform, e.Version, (VersionStatus)e.Status, e.SunsetAt, e.Notes, e.DownloadUrl);
}
