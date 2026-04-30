using ACommerce.Chat.Operations;
using ACommerce.Favorites.Operations.Entities;
using ACommerce.Kits.Support.Domain;
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

    public CatalogController(
        EjarDbContext db,
        IChatService? chat = null,
        IConnectionTracker? connections = null)
    {
        _db = db;
        _chat = chat;
        _connections = connections;
    }

    private Guid? CurrentUserGuid =>
        Guid.TryParse(User.FindFirstValue("user_id"), out var g) ? g : null;
    private string CurrentUserSeedId =>
        User.FindFirstValue("user_id") ?? EjarSeed.CurrentUserId;

    // ═══ Profile ═════════════════════════════════════════════════════════
    [HttpGet("/me/profile")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (CurrentUserGuid is { } id)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (u is null) return this.UnauthorizedEnvelope("user_not_found");
            return this.OkEnvelope("profile.get", new {
                id = u.Id, fullName = u.FullName, phone = u.Phone, email = u.Email,
                city = u.City, memberSince = u.MemberSince, avatar = u.AvatarUrl,
                stats = new { listingsCount = 0, bookingsCount = 0 }
            });
        }
        return this.UnauthorizedEnvelope("user_not_found");
    }

    public sealed record UpdateProfileBody(
        string? FullName, string? Phone, string? Email, string? City, string? AvatarUrl);

    [HttpPut("/me/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileBody body, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } id) return this.UnauthorizedEnvelope();
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return this.UnauthorizedEnvelope("user_not_found");

        if (!string.IsNullOrWhiteSpace(body.FullName)) u.FullName = body.FullName!;
        if (body.Phone     is not null) u.Phone     = body.Phone;
        if (body.Email     is not null) u.Email     = body.Email;
        if (body.City      is not null) u.City      = body.City;
        if (body.AvatarUrl is not null) u.AvatarUrl = body.AvatarUrl;
        u.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return this.OkEnvelope("profile.update",
            new { id = u.Id, fullName = u.FullName, avatar = u.AvatarUrl });
    }

    // ═══ Subscription / Invoices ═════════════════════════════════════════
    [HttpGet("/me/subscription")]
    public async Task<IActionResult> MySubscription(CancellationToken ct)
    {
        if (CurrentUserGuid is not { } id) return this.UnauthorizedEnvelope();
        var s = await _db.Subscriptions.AsNoTracking()
            .Where(x => x.UserId == id && x.Status == "active")
            .OrderByDescending(x => x.EndDate).FirstOrDefaultAsync(ct);
        if (s is null) return this.OkEnvelope<object?>("me.subscription", null);
        return this.OkEnvelope("me.subscription", new {
            id = s.Id, planId = s.PlanId, planName = s.PlanName, status = s.Status,
            startDate = s.StartDate, endDate = s.EndDate,
            listingsLimit = s.ListingsLimit, featuredLimit = s.FeaturedLimit,
            imagesPerListing = s.ImagesPerListing,
            price = 0m
        });
    }

    [HttpGet("/me/invoices")]
    public async Task<IActionResult> MyInvoices(CancellationToken ct)
    {
        if (CurrentUserGuid is not { } id) return this.UnauthorizedEnvelope();
        var rows = await _db.Invoices.AsNoTracking()
            .Where(x => x.UserId == id).OrderByDescending(x => x.Date)
            .Select(x => new { id = x.Id, planId = x.PlanId, amount = x.Amount, date = x.Date, status = x.Status })
            .ToListAsync(ct);
        return this.OkEnvelope("me.invoices", rows);
    }

    // ═══ Subscription activation ═════════════════════════════════════════
    public sealed record ActivateSubscriptionBody(string? PlanId);

    [HttpPost("/subscriptions/activate")]
    public async Task<IActionResult> ActivateSubscription([FromBody] ActivateSubscriptionBody body, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } uid) return this.UnauthorizedEnvelope();
        if (string.IsNullOrWhiteSpace(body.PlanId) || !Guid.TryParse(body.PlanId, out var planId))
            return this.BadRequestEnvelope("invalid_plan_id");

        var plan = await _db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return this.NotFoundEnvelope("plan_not_found");

        // إنهاء أيّ اشتراك سابق نشط للمستخدم (subscription واحد فعّال في كلّ وقت).
        var prior = await _db.Subscriptions
            .Where(s => s.UserId == uid && s.Status == "active")
            .ToListAsync(ct);
        foreach (var p in prior)
        {
            p.Status = "expired";
            p.UpdatedAt = DateTime.UtcNow;
        }

        var sub = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UserId = uid,
            PlanId = plan.Id,
            PlanName = plan.Label,
            Status = "active",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(1),
            ListingsLimit = plan.MaxActiveListings,
            FeaturedLimit = plan.MaxFeaturedListings,
            ImagesPerListing = plan.MaxImagesPerListing,
        };
        _db.Subscriptions.Add(sub);

        // فاتورة مرافقة (mock — ندوّن خصماً ناجحاً).
        _db.Invoices.Add(new InvoiceEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UserId = uid,
            PlanId = plan.Id,
            Amount = plan.Price,
            Date = DateTime.UtcNow,
            Status = "paid",
        });

        await _db.SaveChangesAsync(ct);
        return this.OkEnvelope("subscription.activate", new {
            id = sub.Id, planId = sub.PlanId, planName = sub.PlanName,
            status = sub.Status, startDate = sub.StartDate, endDate = sub.EndDate
        });
    }

    // ═══ My Listings ═════════════════════════════════════════════════════
    [HttpGet("/my-listings")]
    public async Task<IActionResult> MyListings(CancellationToken ct)
    {
        if (CurrentUserGuid is not { } id) return this.UnauthorizedEnvelope();
        // مُصغّر الصورة (ThumbnailUrl) ~30KB بدل الصورة الكاملة في ImagesCsv
        // (~250KB لكلّ إعلان). الـ Substring القديم كان buggy: يقتطع من
        // ImagesCsv حتى أوّل ',' لكنّ السواتر هو '|'.
        var rows = await _db.Listings.AsNoTracking()
            .Where(l => l.OwnerId == id).OrderByDescending(l => l.CreatedAt)
            .Select(l => new {
                id = l.Id, title = l.Title, price = l.Price, timeUnit = l.TimeUnit,
                propertyType = l.PropertyType, city = l.City, district = l.District,
                status = l.Status, viewsCount = l.ViewsCount, isVerified = l.IsVerified,
                bedroomCount = l.BedroomCount,
                firstImage = l.ThumbnailUrl
            })
            .ToListAsync(ct);
        return this.OkEnvelope("listing.my", rows);
    }

    public sealed record CreateListingBody(
        string? Title, string? Description, decimal? Price,
        string? TimeUnit, string? PropertyType,
        string? City, string? District,
        double? Lat, double? Lng,
        int? BedroomCount, int? BathroomCount, int? AreaSqm,
        IReadOnlyList<string>? Amenities,
        IReadOnlyList<string>? Images,
        // مُصغّر الصورة الرئيسيّة (data:image/jpeg;base64,...). اختياريّ —
        // لو فاضي (لا صور)، نُخزّنه null والبطاقات لن تظهر صورة.
        string? Thumbnail);

    [HttpPost("/my-listings")]
    public async Task<IActionResult> CreateListing([FromBody] CreateListingBody body, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } id) return this.UnauthorizedEnvelope();
        if (string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.City))
            return this.BadRequestEnvelope("missing_fields", "title و city مطلوبان");

        // فحص الاشتراك: لا يُسمح بإنشاء إعلان بلا اشتراك نشط، ولا تجاوز حصّة
        // الباقة. السابق كان يسمح للجميع بالنشر — يخالف ميثاق الباقات.
        var sub = await _db.Subscriptions
            .Where(s => s.UserId == id && s.Status == "active" && s.EndDate > DateTime.UtcNow)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefaultAsync(ct);
        if (sub is null)
            return this.BadRequestEnvelope("no_active_subscription",
                "لا يوجد اشتراك نشط — اشترك بباقة أوّلاً.");

        var activeCount = await _db.Listings.CountAsync(
            l => l.OwnerId == id && l.Status == 1 && !l.IsDeleted, ct);
        if (sub.ListingsLimit > 0 && activeCount >= sub.ListingsLimit)
            return this.BadRequestEnvelope("listings_quota_exceeded",
                $"الباقة الحاليّة تسمح بـ {sub.ListingsLimit} إعلان نشط فقط — حدّث الباقة أو احذف إعلاناً.");

        // فحص حصّة الصور لكلّ إعلان حسب الباقة.
        var imageCount = body.Images?.Count ?? 0;
        if (sub.ImagesPerListing > 0 && imageCount > sub.ImagesPerListing)
            return this.BadRequestEnvelope("images_quota_exceeded",
                $"الباقة الحاليّة تسمح بـ {sub.ImagesPerListing} صور لكلّ إعلان.");

        var entity = new ListingEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Title = body.Title!, Description = body.Description ?? "",
            Price = body.Price ?? 0,
            TimeUnit = body.TimeUnit ?? "monthly",
            PropertyType = body.PropertyType ?? "apartment",
            City = body.City!, District = body.District ?? "",
            Lat = body.Lat ?? 0, Lng = body.Lng ?? 0,
            OwnerId = id,
            BedroomCount = body.BedroomCount ?? 0,
            BathroomCount = body.BathroomCount ?? 0,
            AreaSqm = body.AreaSqm ?? 0,
            Status = 1,
            ImagesCsv = body.Images is null ? "" : string.Join("|", body.Images),
            ThumbnailUrl = string.IsNullOrEmpty(body.Thumbnail) ? null : body.Thumbnail,
            AmenitiesCsv = body.Amenities is null ? "" : string.Join(",", body.Amenities),
        };
        _db.Listings.Add(entity);
        await _db.SaveChangesAsync(ct);
        return this.OkEnvelope("listing.create", new { id = entity.Id, title = entity.Title, status = entity.Status });
    }

    [HttpPost("/my-listings/{id:guid}/toggle")]
    public async Task<IActionResult> ToggleListing(Guid id, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } uid) return this.UnauthorizedEnvelope();
        var l = await _db.Listings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return this.NotFoundEnvelope("listing_not_found");
        if (l.OwnerId != uid) return this.ForbiddenEnvelope("not_owner");
        l.Status = l.Status == 1 ? 2 : 1;
        l.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return this.OkEnvelope("listing.toggle", new { id = l.Id, status = l.Status });
    }

    [HttpDelete("/my-listings/{id:guid}")]
    public async Task<IActionResult> DeleteListing(Guid id, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } uid) return this.UnauthorizedEnvelope();
        var l = await _db.Listings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return this.NotFoundEnvelope("listing_not_found");
        if (l.OwnerId != uid) return this.ForbiddenEnvelope("not_owner");
        l.IsDeleted = true;
        l.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return this.OkEnvelope("listing.delete", new { id, deleted = true });
    }

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

    // ═══ Complaints (bridge for the frontend's /complaints.* endpoints) ══
    [HttpGet("/complaints")]
    public async Task<IActionResult> Complaints(CancellationToken ct)
    {
        if (CurrentUserGuid is not { } uid) return this.UnauthorizedEnvelope();
        var rows = await _db.Complaints.AsNoTracking()
            .Where(t => t.UserId == uid).OrderByDescending(t => t.CreatedAt)
            .Select(t => new {
                id = t.Id, subject = t.Subject, status = t.Status, priority = t.Priority,
                createdAt = t.CreatedAt
            }).ToListAsync(ct);
        return this.OkEnvelope("complaint.list", rows);
    }

    [HttpGet("/complaints/{id:guid}")]
    public async Task<IActionResult> ComplaintDetail(Guid id, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } uid) return this.UnauthorizedEnvelope();
        var t = await _db.Complaints.AsNoTracking()
            .Include(x => x.Replies).FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct);
        if (t is null) return this.NotFoundEnvelope("complaint_not_found");
        return this.OkEnvelope("complaint.details", new {
            id = t.Id, subject = t.Subject, body = t.Body,
            status = t.Status, priority = t.Priority, createdAt = t.CreatedAt,
            replies = t.Replies.OrderBy(r => r.CreatedAt).Select(r => new {
                id = r.Id, from = r.FromRole, message = r.Message, createdAt = r.CreatedAt
            })
        });
    }

    public sealed record FileComplaintBody(string? Subject, string? Body, string? Priority);

    [HttpPost("/complaints")]
    public async Task<IActionResult> FileComplaint([FromBody] FileComplaintBody body, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } uid) return this.UnauthorizedEnvelope();
        if (string.IsNullOrWhiteSpace(body.Subject) || string.IsNullOrWhiteSpace(body.Body))
            return this.BadRequestEnvelope("missing_fields", "subject و body مطلوبان");
        var t = new SupportTicket {
            Id = Guid.NewGuid(), UserId = uid,
            Subject = body.Subject!, Body = body.Body!,
            Status = "open", Priority = body.Priority ?? "عادي",
            CreatedAt = DateTime.UtcNow
        };
        _db.Complaints.Add(t);
        await _db.SaveChangesAsync(ct);
        return this.OkEnvelope("complaint.file", new { id = t.Id, status = t.Status });
    }

    public sealed record ReplyBody(string? Message);

    [HttpPost("/complaints/{id:guid}/replies")]
    public async Task<IActionResult> ReplyComplaint(Guid id, [FromBody] ReplyBody body, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } uid) return this.UnauthorizedEnvelope();
        if (string.IsNullOrWhiteSpace(body.Message))
            return this.BadRequestEnvelope("missing_message");
        var t = await _db.Complaints.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct);
        if (t is null) return this.NotFoundEnvelope("complaint_not_found");
        var r = new SupportReply {
            Id = Guid.NewGuid(), TicketId = t.Id,
            FromRole = "user", AuthorId = uid, Message = body.Message!,
            CreatedAt = DateTime.UtcNow
        };
        _db.ComplaintReplies.Add(r);
        await _db.SaveChangesAsync(ct);
        return this.OkEnvelope("complaint.reply", new { id = r.Id });
    }

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
