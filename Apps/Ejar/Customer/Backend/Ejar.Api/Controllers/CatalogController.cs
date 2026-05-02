using ACommerce.Chat.Operations;
using ACommerce.Favorites.Operations.Entities;
using ACommerce.Kits.Support.Domain;
using ACommerce.Notification.Providers.Firebase.Storage;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.Realtime.Operations.Abstractions;
using Ejar.Api.Data;
using Ejar.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ejar.Api.Controllers;

/// <summary>
/// نقاط النهاية الخاصّة بالمستخدم الحاليّ (تتطلّب مصادقة) — مسارات يتوقّعها
/// التطبيق المشترك مباشرةً (راجع <c>docs/EJAR-API-CONTRACT.md</c>).
/// تستخدم <c>EjarDbContext</c> حيثما توفّرت البيانات في القاعدة، و<c>EjarSeed</c>
/// كـ fallback لمحاكاة (المفضلات، الحجوزات، الاشتراكات…) ريثما تُبنى الكيانات
/// الفعليّة.
/// </summary>
[ApiController, Authorize]
public sealed class CatalogController : ControllerBase
{
    private readonly EjarDbContext _db;
    private readonly IChatService? _chat;
    private readonly IConnectionTracker? _connections;
    private readonly IDeviceTokenStore? _pushTokens;
    private readonly IConfiguration _config;

    public CatalogController(
        EjarDbContext db,
        IConfiguration config,
        IChatService? chat = null,
        IConnectionTracker? connections = null,
        IDeviceTokenStore? pushTokens = null)
    {
        _db = db;
        _config = config;
        _chat = chat;
        _connections = connections;
        _pushTokens = pushTokens;
    }

    /// <summary>
    /// وضع التجربة المفتوحة — يُعطّل بوّابات الاشتراك (no_active_subscription،
    /// listings_quota_exceeded، images_quota_exceeded). افتراضيّاً <c>true</c>
    /// في فترة الإطلاق التجريبيّ. يُضبط من appsettings عبر <c>Trial:OpenAccess</c>.
    /// </summary>
    private bool TrialOpenAccess =>
        _config.GetValue<bool?>("Trial:OpenAccess") ?? true;

    // ═══ Push subscription ═══════════════════════════════════════════════
    public sealed record PushSubscribeBody(string? Token, string? Platform);

    /// <summary>
    /// تسجيل رمز جهاز لـ Firebase Cloud Messaging. الواجهة تستدعيها بعد ما
    /// يحصل المتصفّح/التطبيق على الـ token من Firebase SDK
    /// (<c>getToken({vapidKey})</c>). الرموز تُحفَظ في جدول
    /// <c>UserPushTokens</c> ويستهلكها <c>FirebaseNotificationChannel</c>
    /// عند البثّ. لو الـ FCM غير مهيّأ على الخادم، نُرجع 200 ok بصمت ليبقى
    /// الفرونت غير مرتبط بحالة التهيئة.
    /// </summary>
    [HttpPost("/me/push-subscription")]
    public async Task<IActionResult> RegisterPushToken([FromBody] PushSubscribeBody body, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } uid) return this.UnauthorizedEnvelope();
        if (string.IsNullOrWhiteSpace(body.Token))
            return this.BadRequestEnvelope("missing_token");
        if (_pushTokens is null)
            return this.OkEnvelope("push.subscribe", new { ok = true, fcmConfigured = false });

        await _pushTokens.RegisterAsync(uid.ToString(), body.Token!, body.Platform, ct);
        return this.OkEnvelope("push.subscribe", new { ok = true, fcmConfigured = true });
    }

    [HttpDelete("/me/push-subscription/{token}")]
    public async Task<IActionResult> UnregisterPushToken(string token, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } _) return this.UnauthorizedEnvelope();
        if (_pushTokens is not null) await _pushTokens.UnregisterAsync(token, ct);
        return this.OkEnvelope("push.unsubscribe", new { ok = true });
    }

    private Guid? CurrentUserGuid =>
        Guid.TryParse(User.FindFirstValue("user_id"), out var g) ? g : null;
    private string CurrentUserSeedId =>
        User.FindFirstValue("user_id") ?? EjarSeed.CurrentUserId;

    // ═══ Profile  →  نُقل لـ Profiles.Backend kit (ProfilesController).
    //   GET/PUT /me/profile. EjarProfileStore يترجم بين IUserProfile و
    //   UserEntity.

    // ═══ Subscription / Invoices / Activation ═══════════════════════════
    // نُقلت لـ Subscriptions.Backend kit (SubscriptionsController،
    // PlansController، InvoicesController). Trial.OpenAccess صار خياراً
    // داخل الكيت — يُمرَّر من Program.cs عبر AddSubscriptionsKit(opts).
    // EjarSubscriptionStore + EjarPlanStore + EjarInvoiceStore تترجم
    // الـ EF entities للـ views التي يُسلِّمها الكيت.

    // ═══ My Listings / Listings — كلّ المسارات نُقلت لـ Listings.Backend kit
    //   (GET /listings ، GET /listings/{id} ، GET /home/explore ، GET /my-listings ،
    //    POST /my-listings ، PATCH /my-listings/{id} ، POST /my-listings/{id}/toggle،
    //    DELETE /my-listings/{id}).  EjarListingStore يترجم بين IListing و
    //    ListingEntity. بوّابة الـ subscription/quota اختفت (Trial.OpenAccess=true
    //    افتراضيّاً) — لو احتاجها التطبيق لاحقاً، تُضاف interceptor على
    //    listing.create يقرأ ISubscriptionStore.

    // ═══ Favorites ═══════════════════════════════════════════════════════
    [HttpGet("/favorites")]
    public async Task<IActionResult> Favorites(CancellationToken ct)
    {
        if (CurrentUserGuid is not { } uid) return this.UnauthorizedEnvelope();
        var ids = await _db.Favorites.AsNoTracking()
            .Where(f => f.UserId == uid && f.EntityType == nameof(ListingEntity))
            .Select(f => f.EntityId).ToListAsync(ct);
        var listings = await _db.Listings.AsNoTracking()
            .Where(l => ids.Contains(l.Id)).ToListAsync(ct);
        return this.OkEnvelope("favorite.list",
            listings.Select(l => new {
                id = l.Id, title = l.Title, price = l.Price,
                timeUnit = l.TimeUnit, propertyType = l.PropertyType,
                city = l.City, district = l.District, isVerified = l.IsVerified,
                bedroomCount = l.BedroomCount,
                // فضّل المُصغّر للبطاقات (~30KB) على الصورة الكاملة (~250KB).
                // fallback على أوّل صورة في ImagesCsv للإعلانات القديمة قبل
                // إضافة ThumbnailUrl.
                firstImage = l.ThumbnailUrl ?? l.ImagesCsv?.Split('|').FirstOrDefault()
            }));
    }

    [HttpPost("/listings/{id:guid}/favorite")]
    public async Task<IActionResult> ToggleFavorite(Guid id, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } uid) return this.UnauthorizedEnvelope();
        var listing = await _db.Listings.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null) return this.NotFoundEnvelope("listing_not_found");

        var existing = await _db.Favorites.FirstOrDefaultAsync(
            f => f.UserId == uid && f.EntityId == id && f.EntityType == nameof(ListingEntity), ct);
        bool nowFavorite;
        if (existing is null)
        {
            _db.Favorites.Add(new Favorite {
                Id = Guid.NewGuid(), UserId = uid,
                EntityId = id, EntityType = nameof(ListingEntity),
                CreatedAt = DateTime.UtcNow
            });
            nowFavorite = true;
        }
        else
        {
            _db.Favorites.Remove(existing);
            nowFavorite = false;
        }
        await _db.SaveChangesAsync(ct);
        return this.OkEnvelope("favorite.toggle", new { id, isFavorite = nowFavorite });
    }

    // ═══ Bookings ════════════════════════════════════════════════════════
    [HttpGet("/bookings")]
    public IActionResult Bookings() =>
        this.OkEnvelope("booking.list", Array.Empty<object>());

    [HttpGet("/bookings/{id}")]
    public IActionResult BookingDetails(string id) =>
        this.NotFoundEnvelope("booking_not_found");

    // ═══ الشكاوى انتقلت إلى Support kit ═══════════════════════════════
    // المسارات الجديدة (راجع libs/kits/Support/.../SupportController.cs):
    //   GET    /support/tickets
    //   GET    /support/tickets/{id}    ← يُرجع التذكرة + رسائل المحادثة
    //   POST   /support/tickets
    //   POST   /support/tickets/{id}/replies
    //   PATCH  /support/tickets/{id}/status
    // الـ controller الجديد يستهلك ISupportStore (EjarSupportStore) +
    // IChatStore، فيرث realtime + DB notification + FCM push من مسار
    // chat.message تلقائياً، ضمن envelope OAM واحد لكل عمليّة.

    // ═══ Conversations / Chat (start a new conversation) ═════════════════
    // العميل يرسل ListingId فقط (+ نصّ ابتدائيّ اختياريّ). الخادم يستنبط مالك
    // الإعلان من جدول Listings ويستعمله partner. PartnerId يُقبل لو أُرسل صراحةً
    // (override للحالات الإداريّة) لكنّه ليس مطلوباً.
    public sealed record StartConversationBody(string? ListingId, string? PartnerId, string? Text);

    [HttpPost("/conversations/start")]
    public async Task<IActionResult> StartConversation([FromBody] StartConversationBody body, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } uid) return this.UnauthorizedEnvelope();
        if (string.IsNullOrWhiteSpace(body.ListingId) || !Guid.TryParse(body.ListingId, out var listingId))
            return this.BadRequestEnvelope("missing_or_invalid_listing_id");

        var listing = await _db.Listings.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == listingId, ct);
        if (listing is null) return this.NotFoundEnvelope("listing_not_found");

        // partnerId: explicit override من body، أو مالك الإعلان (الافتراضيّ).
        var partnerId = !string.IsNullOrWhiteSpace(body.PartnerId)
            && Guid.TryParse(body.PartnerId, out var explicitPartner)
            ? explicitPartner
            : listing.OwnerId;

        // لا يُسمح بمحادثة الذات.
        if (partnerId == uid) return this.BadRequestEnvelope("cannot_chat_with_self");

        // إيجاد محادثة سابقة لنفس الطرفَين على نفس الإعلان — بأيّ اتجاه.
        // ليس فقط (Owner=me, Partner=them): قد يكون الآخر بدأها أوّلاً وأصبح
        // Owner. نطابق على الأزواج بصرف النظر عن الترتيب.
        var existing = await _db.Conversations.FirstOrDefaultAsync(
            c => c.ListingId == listingId &&
                 ((c.OwnerId == uid && c.PartnerId == partnerId) ||
                  (c.OwnerId == partnerId && c.PartnerId == uid)),
            ct);
        if (existing is not null)
        {
            await AppendInitialMessageIfAny(existing.Id, uid, body.Text, ct);
            return this.OkEnvelope("conversation.start", new { id = existing.Id, created = false });
        }

        var partner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == partnerId, ct);
        var conv = new ConversationEntity {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            OwnerId = uid,
            PartnerId = partnerId, ListingId = listingId,
            PartnerName = partner?.FullName ?? "—",
            Subject = listing.Title,
            LastAt = DateTime.UtcNow,
            UnreadCount = 0
        };
        _db.Conversations.Add(conv);
        await _db.SaveChangesAsync(ct);

        // اشترك الطرفَين في notif:conv:X الآن لو هما متّصلَين بـ realtime —
        // الـ Hub يفعل ذلك على اتصال جديد، لكن لو الطرف الآخر متّصل بالفعل
        // بدون أن تكن المحادثة موجودة وقت اتصاله، لن يكون مشتركاً. هذا
        // الاشتراك هنا يُغلق الفجوة فورَ الإنشاء.
        await SubscribeBothPartiesAsync(conv.Id, uid, partnerId, ct);

        await AppendInitialMessageIfAny(conv.Id, uid, body.Text, ct);
        return this.OkEnvelope("conversation.start", new { id = conv.Id, created = true });
    }

    private async Task SubscribeBothPartiesAsync(Guid convId, Guid a, Guid b, CancellationToken ct)
    {
        if (_chat is null || _connections is null) return;
        var convStr = convId.ToString();
        // الـ tracker وقنوات الـ Chat Kit تستعمل partyId ("User:{guid}") لا
        // raw guid (مطابق لـ EjarRealtimeHub.OnConnectedAsync و
        // ChatController.CallerPartyId). الاستعلام بدون البادئة لا يجد شيئاً.
        foreach (var uid in new[] { a, b })
        {
            try
            {
                var partyId = $"User:{uid}";
                var connId = await _connections.GetConnectionIdAsync(partyId, ct);
                if (string.IsNullOrEmpty(connId)) continue; // غير متّصل الآن
                await _chat.SubscribeUserAsync(convStr, partyId, connId, ct);
            }
            catch { /* لا نكسر إنشاء المحادثة لو فشل اشتراك واحد */ }
        }
    }

    // ملاحظة: GET /conversations و GET /conversations/{id} يعرّفهما
    // ACommerce.Kits.Chat.Backend.ChatController. كنّا نُعرّفهما أيضاً هنا
    // فحدث route conflict (AmbiguousMatchException) فيعطي ASP.NET 504/500
    // للـ inbox والتفاصيل. الآن EjarCustomerChatStore يقرأ من EF فيُغذّيهما
    // بنفس البيانات + حقول إضافيّة (Owner/Partner/Subject/ListingId).

    private async Task AppendInitialMessageIfAny(Guid conversationId, Guid senderId, string? text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _db.Messages.Add(new MessageEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ConversationId = conversationId,
            From = senderId.ToString(),
            Text = text!,
            SentAt = DateTime.UtcNow,
        });
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conv is not null)
        {
            conv.LastAt = DateTime.UtcNow;
            conv.UnreadCount += 1;
        }
        await _db.SaveChangesAsync(ct);
    }
}
