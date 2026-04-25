using ACommerce.Chat.Operations;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.Realtime.Operations.Abstractions;
using Ashare.V2.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.V2.Api.Controllers;

/// <summary>
/// Endpoints إضافيّة لـ Ashare.V2: cities, plans, legal, version, profile,
/// subscription, complaints thread.
///
/// المنهجيّة: كلّ GET يعيد OkEnvelope (قراءة لا تغيّر الحالة).
/// كلّ POST/PUT/DELETE يُبنى كـ <c>Entry.Create(...)</c> ويمرّ عبر OpEngine:
///   - OwnershipInterceptor يفحص tag "owner_policy"
///   - ListingQuotaInterceptor يفحص tag "quota_listing"
/// </summary>
[ApiController]
public class CatalogController : ControllerBase
{
    private readonly OpEngine _engine;
    private readonly IChatService? _chat;
    private readonly IConnectionTracker? _connections;

    private static readonly TimeSpan ChatIdleTimeout = TimeSpan.FromMinutes(2);

    public CatalogController(
        OpEngine engine,
        IChatService? chat = null,
        IConnectionTracker? connections = null)
    {
        _engine      = engine;
        _chat        = chat;
        _connections = connections;
    }

    // Complaints replies live here in-memory so POST creates are visible immediately.
    private static readonly List<AshareV2Seed.ComplaintSeed> _complaintsMutable =
        AshareV2Seed.Complaints.ToList();

    private string CurrentUserId => HttpContext.Items["user_id"] as string ?? AshareV2Seed.CurrentUserId;
    private string Caller => $"User:{CurrentUserId}";

    [HttpGet("/cities")]
    public IActionResult Cities() =>
        this.OkEnvelope("cities.list", AshareV2Seed.Cities);

    [HttpGet("/amenities")]
    public IActionResult Amenities() =>
        this.OkEnvelope("amenities.list",
            AshareV2Seed.Amenities.Select(k => new {
                key = k, label = AshareV2Seed.AmenityLabels.GetValueOrDefault(k, k)
            }));

    [HttpGet("/plans")]
    public IActionResult Plans() =>
        this.OkEnvelope("plans.list",
            AshareV2Seed.Plans.Select(p => new {
                id = p.Id, name = p.Name, description = p.Description,
                price = p.Price, unit = p.Unit,
                listingQuota = p.ListingQuota, featuredQuota = p.FeaturedQuota,
                imagesPerListing = p.ImagesPerListing,
                popular = p.Popular, features = p.Features
            }));

    [HttpGet("/legal/{key}")]
    public IActionResult Legal(string key)
    {
        var doc = AshareV2Seed.Legal.FirstOrDefault(l => l.Key == key);
        if (doc is null) return this.NotFoundEnvelope("legal_not_found", $"Key '{key}' not found");
        return this.OkEnvelope("legal.fetch", new { key = doc.Key, title = doc.Title, body = doc.Body });
    }

    [HttpGet("/legal")]
    public IActionResult LegalAll() =>
        this.OkEnvelope("legal.list",
            AshareV2Seed.Legal.Select(d => new { key = d.Key, title = d.Title, body = d.Body }));

    [HttpGet("/version/check")]
    public IActionResult VersionCheck() =>
        this.OkEnvelope("app.version.check", new {
            current  = AshareV2Seed.Version.Current,
            latest   = AshareV2Seed.Version.Latest,
            isBlocked= AshareV2Seed.Version.IsBlocked,
            storeUrl = AshareV2Seed.Version.StoreUrl,
            supportEmail = AshareV2Seed.Version.SupportEmail
        });

    // ── Bookings ───────────────────────────────────────────────────────
    [HttpGet("/bookings")]
    public IActionResult Bookings() =>
        this.OkEnvelope("booking.list",
            AshareV2Seed.Bookings.Select(b => new {
                id = b.Id, listingId = b.ListingId, listingTitle = b.ListingTitle,
                total = b.Total, startDate = b.StartDate, nights = b.Nights,
                guests = b.Guests, status = b.Status
            }));

    [HttpGet("/bookings/{id}")]
    public IActionResult Booking(string id)
    {
        var b = AshareV2Seed.Bookings.FirstOrDefault(x => x.Id == id);
        if (b is null) return this.NotFoundEnvelope("booking_not_found");
        return this.OkEnvelope("booking.details", new {
            id = b.Id, listingId = b.ListingId, listingTitle = b.ListingTitle,
            total = b.Total, startDate = b.StartDate, nights = b.Nights,
            guests = b.Guests, status = b.Status
        });
    }

    public sealed record CreateBookingRequest(string ListingId, DateTime StartDate, int Nights, int Guests);
    /// <summary>
    /// إنشاء حجز. السياسات:
    ///   - owner_policy = must_not_own → لا تحجز إعلانك
    ///   - status == 1 فحص محلّيّ (not an ownership concern)
    /// </summary>
    [HttpPost("/bookings")]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest req, CancellationToken ct)
    {
        var listing = AshareV2Seed.Listings.FirstOrDefault(l => l.Id == req.ListingId);
        if (listing is null) return this.NotFoundEnvelope("listing_not_found");
        if (listing.Status != 1)
            return this.BadRequestEnvelope("listing_inactive", "الإعلان غير نشط حاليّاً");

        var total = listing.Price * req.Nights;
        var bookingId = $"B-{Random.Shared.Next(10_000, 99_999)}";
        var payload = new {
            id = bookingId,
            listingId = req.ListingId, listingTitle = listing.Title,
            total, startDate = req.StartDate, nights = req.Nights, guests = req.Guests,
            status = "pending"
        };

        var op = Entry.Create("booking.create")
            .Describe($"User books '{listing.Title}' × {req.Nights} nights")
            .From(Caller, 1, ("role","booker"))
            .To($"Listing:{req.ListingId}", 1, ("role","booked"))
            .Tag("listing_id", req.ListingId)
            .Tag("booking_id", bookingId)
            .Tag("owner_policy", "must_not_own")
            .Tag("resource_owner", listing.OwnerId)
            .Analyze(new RangeAnalyzer("nights", () => req.Nights, 1, 365))
            .Analyze(new RangeAnalyzer("guests", () => req.Guests, 1, 20))
            .Execute(ctx => Task.CompletedTask)   // in-memory: مجرّد الإعلان يكفي للعرض
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, payload, ct);
        if (env.Operation.Status != "Success")
            return this.ForbiddenEnvelope(env.Operation.FailedAnalyzer ?? "booking_failed",
                env.Operation.ErrorMessage);
        return this.OkEnvelope("booking.create", payload);
    }

    // ── Chats ──────────────────────────────────────────────────────────
    [HttpGet("/conversations")]
    public IActionResult Conversations() =>
        this.OkEnvelope("conversation.list",
            AshareV2Seed.Conversations.Select(c => new {
                id = c.Id, partnerName = c.PartnerName,
                partnerId = c.PartnerId, listingId = c.ListingId,
                subject = c.Subject, lastAt = c.LastAt, unreadCount = c.UnreadCount,
                lastMessage = c.Messages.LastOrDefault()?.Text ?? ""
            }));

    [HttpGet("/conversations/{id}")]
    public IActionResult Conversation(string id)
    {
        var c = AshareV2Seed.Conversations.FirstOrDefault(x => x.Id == id);
        if (c is null) return this.NotFoundEnvelope("conversation_not_found");
        return this.OkEnvelope("conversation.details", new {
            id = c.Id, partnerName = c.PartnerName,
            partnerId = c.PartnerId, listingId = c.ListingId,
            subject = c.Subject,
            messages = c.Messages.Select(m => new {
                id = m.Id, from = m.From, text = m.Text, sentAt = m.SentAt
            })
        });
    }

    public sealed record SendMessageRequest(string Text, string? Attachment);
    /// <summary>إرسال رسالة إلى محادثة موجودة (السيرفر يختم SentAt = UTC).</summary>
    [HttpPost("/conversations/{id}/messages")]
    public async Task<IActionResult> SendMessage(string id, [FromBody] SendMessageRequest req, CancellationToken ct)
    {
        var ix = AshareV2Seed.Conversations.FindIndex(c => c.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("conversation_not_found");
        var conv = AshareV2Seed.Conversations[ix];
        var msg  = new AshareV2Seed.MessageSeed(
            Id:             $"m-{conv.Messages.Count + 1}",
            ConversationId: id,
            From:           "me",
            Text:           req.Text ?? string.Empty,
            SentAt:         DateTime.UtcNow);

        var op = Entry.Create("message.send")
            .Describe($"User sends message to conversation {id}")
            .From(Caller, 1, ("role","sender"))
            .To($"Conversation:{id}", 1, ("role","appended"))
            .Tag("conversation_id", id)
            .Analyze(new MaxLengthAnalyzer("text", () => req.Text, 4000))
            .Execute(ctx =>
            {
                conv.Messages.Add(msg);
                AshareV2Seed.Conversations[ix] = conv with { LastAt = msg.SentAt, UnreadCount = 0 };
                return Task.CompletedTask;
            })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, (IChatMessage)msg, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "send_failed", env.Operation.ErrorMessage);

        // Push on both chat:conv:X and notif:conv:X — partner gets whichever
        // they're subscribed to (chat if their ChatRoom is open, else notif).
        if (_chat is not null) await _chat.BroadcastNewMessageAsync(msg, CancellationToken.None);

        return this.OkEnvelope("message.send", msg);
    }

    // ─── Chat channel lifecycle (frontend ChatRoom calls these) ─────────────

    [HttpPost("/chat/{convId}/enter")]
    public async Task<IActionResult> EnterChat(string convId, CancellationToken ct)
    {
        if (_chat is null) return this.OkEnvelope("chat.enter", new { ok = true });
        var connId = _connections is null ? null : await _connections.GetConnectionIdAsync(CurrentUserId, ct);
        if (string.IsNullOrEmpty(connId))
            return this.OkEnvelope("chat.enter", new { ok = false, reason = "no_connection" });
        await _chat.EnterConversationAsync(convId, CurrentUserId, connId, ChatIdleTimeout, ct);
        return this.OkEnvelope("chat.enter", new { ok = true, conversationId = convId });
    }

    [HttpPost("/chat/{convId}/leave")]
    public async Task<IActionResult> LeaveChat(string convId, CancellationToken ct)
    {
        if (_chat is not null) await _chat.LeaveConversationAsync(convId, CurrentUserId, ct);
        return this.OkEnvelope("chat.leave", new { ok = true, conversationId = convId });
    }

    public sealed record StartConversationRequest(string ListingId, string Text);
    /// <summary>
    /// فتح محادثة مع مالك إعلان.
    /// السياسة: OwnershipInterceptor يفرض must_not_own (لا تراسل إعلانك).
    /// </summary>
    [HttpPost("/conversations/start")]
    public async Task<IActionResult> StartConversation([FromBody] StartConversationRequest req, CancellationToken ct)
    {
        var listing = AshareV2Seed.Listings.FirstOrDefault(l => l.Id == req.ListingId);
        if (listing is null) return this.NotFoundEnvelope("listing_not_found");

        // إن كانت المحادثة موجودة مع نفس المالك، أعدها (بدون تنفيذ operation).
        var existing = AshareV2Seed.Conversations.FirstOrDefault(c =>
            c.PartnerId == listing.OwnerId && c.ListingId == listing.Id);
        string newId = existing?.Id ?? $"C-{AshareV2Seed.Conversations.Count + 1}";

        var op = Entry.Create("conversation.start")
            .Describe($"User opens chat on listing {listing.Id}")
            .From(Caller, 1, ("role","initiator"))
            .To($"Listing:{listing.Id}", 1, ("role","subject"))
            .Tag("listing_id", listing.Id)
            .Tag("owner_policy", "must_not_own")
            .Tag("resource_owner", listing.OwnerId)
            .Execute(ctx =>
            {
                if (existing is not null) return Task.CompletedTask;
                var conv = new AshareV2Seed.ConversationSeed(
                    newId, "مالك " + listing.Title, listing.Title, DateTime.UtcNow, 0,
                    partnerId: listing.OwnerId, listingId: listing.Id,
                    messages: new List<AshareV2Seed.MessageSeed>
                    {
                        new("m-1", "me", req.Text, DateTime.UtcNow)
                    });
                AshareV2Seed.Conversations.Add(conv);
                return Task.CompletedTask;
            })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op,
            new { id = newId, created = existing is null }, ct);
        if (env.Operation.Status != "Success")
            return this.ForbiddenEnvelope(env.Operation.FailedAnalyzer ?? "conversation_failed",
                env.Operation.ErrorMessage);
        return this.OkEnvelope("conversation.start", new { id = newId, created = existing is null });
    }

    // ── Complaints (list + details + replies + create) ─────────────────
    [HttpGet("/complaints")]
    public IActionResult Complaints() =>
        this.OkEnvelope("complaint.list",
            _complaintsMutable.Select(c => new {
                id = c.Id, subject = c.Subject, body = c.Body,
                createdAt = c.CreatedAt, status = c.Status,
                priority = c.Priority, relatedEntity = c.RelatedEntity,
                repliesCount = c.Replies.Count
            }));

    [HttpGet("/complaints/{id}")]
    public IActionResult ComplaintDetails(string id)
    {
        var c = _complaintsMutable.FirstOrDefault(x => x.Id == id);
        if (c is null) return this.NotFoundEnvelope("complaint_not_found");
        return this.OkEnvelope("complaint.details", new {
            id = c.Id, subject = c.Subject, body = c.Body,
            createdAt = c.CreatedAt, status = c.Status,
            priority = c.Priority, relatedEntity = c.RelatedEntity,
            replies = c.Replies.Select(r => new {
                id = r.Id, from = r.From, message = r.Message, createdAt = r.CreatedAt
            })
        });
    }

    public sealed record CreateComplaintRequest(string Subject, string Body, string? Priority, string? RelatedEntity);
    [HttpPost("/complaints")]
    public async Task<IActionResult> CreateComplaint([FromBody] CreateComplaintRequest req, CancellationToken ct)
    {
        var id = $"X-{_complaintsMutable.Count + 1:D3}";
        var c = new AshareV2Seed.ComplaintSeed(
            id, req.Subject, req.Body, DateTime.UtcNow, "open",
            req.Priority ?? "عادي", req.RelatedEntity ?? "",
            new List<AshareV2Seed.ComplaintReplySeed> {
                new("R1", "user", req.Body, DateTime.UtcNow)
            });

        var op = Entry.Create("complaint.file")
            .Describe($"User files complaint: {req.Subject}")
            .From(Caller, 1, ("role","complainant"))
            .To($"Complaint:{id}", 1, ("role","filed"))
            .Tag("complaint_id", id)
            .Analyze(new RequiredFieldAnalyzer("subject", () => req.Subject))
            .Analyze(new RequiredFieldAnalyzer("body",    () => req.Body))
            .Analyze(new MaxLengthAnalyzer("subject",     () => req.Subject, 200))
            .Analyze(new MaxLengthAnalyzer("body",        () => req.Body, 2000))
            .Execute(ctx => { _complaintsMutable.Insert(0, c); return Task.CompletedTask; })
            .Build();

        var complaintData = new { id = c.Id, subject = c.Subject, status = c.Status, createdAt = c.CreatedAt };
        var env = await _engine.ExecuteEnvelopeAsync(op, complaintData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "complaint_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("complaint.file", complaintData);
    }

    public sealed record ReplyRequest(string Message);
    [HttpPost("/complaints/{id}/replies")]
    public async Task<IActionResult> AddReply(string id, [FromBody] ReplyRequest req, CancellationToken ct)
    {
        var ix = _complaintsMutable.FindIndex(x => x.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("complaint_not_found");

        var op = Entry.Create("complaint.reply")
            .Describe($"User replies on complaint {id}")
            .From(Caller, 1, ("role","replier"))
            .To($"Complaint:{id}", 1, ("role","replied"))
            .Tag("complaint_id", id)
            .Analyze(new RequiredFieldAnalyzer("message", () => req.Message))
            .Analyze(new MaxLengthAnalyzer("message",     () => req.Message, 2000))
            .Execute(ctx =>
            {
                var c = _complaintsMutable[ix];
                var newReplies = c.Replies.Append(new AshareV2Seed.ComplaintReplySeed(
                    $"R{c.Replies.Count + 1}", "user", req.Message, DateTime.UtcNow)).ToList();
                _complaintsMutable[ix] = c with { Replies = newReplies };
                return Task.CompletedTask;
            })
            .Build();

        var replyData = new { id, repliesCount = _complaintsMutable[ix].Replies.Count + 1 };
        var env = await _engine.ExecuteEnvelopeAsync(op, replyData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "reply_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("complaint.reply", replyData);
    }

    [HttpGet("/my-listings")]
    public IActionResult MyListings() =>
        this.OkEnvelope("listing.my",
            AshareV2Seed.Listings
                .Where(l => l.OwnerId == CurrentUserId)
                .Select(l => new {
                    id = l.Id, title = l.Title, price = l.Price, currency = "SAR",
                    timeUnit = l.TimeUnit, city = l.City, district = l.District,
                    isFeatured = l.IsFeatured, status = l.Status,
                    viewsCount = l.ViewsCount, bookingsCount = l.BookingsCount
                }));

    /// <summary>
    /// تبديل حالة إعلان نشط/موقوف.
    /// السياسة: OwnershipInterceptor يفرض must_own عبر tag "owner_policy".
    /// </summary>
    [HttpPost("/my-listings/{id}/toggle")]
    public async Task<IActionResult> ToggleListing(string id, CancellationToken ct)
    {
        var ix = AshareV2Seed.Listings.FindIndex(l => l.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("listing_not_found");
        var l = AshareV2Seed.Listings[ix];
        var newStatus = l.Status == 1 ? 2 : 1;

        var op = Entry.Create("listing.toggle")
            .Describe($"Owner toggles listing {id} to status {newStatus}")
            .From(Caller, 1, ("role","owner"))
            .To($"Listing:{id}", 1, ("role","target"))
            .Tag("listing_id", id)
            .Tag("owner_policy", "must_own")
            .Tag("resource_owner", l.OwnerId)
            .Execute(ctx =>
            {
                AshareV2Seed.Listings[ix] = new AshareV2Seed.ListingSeed(
                    l.Id, l.Title, l.Description, l.Price, l.TimeUnit, l.City, l.District,
                    l.Lat, l.Lng, l.Amenities,
                    ownerId: l.OwnerId, featured: l.IsFeatured, capacity: l.Capacity,
                    rating: l.Rating, categoryId: l.CategoryId,
                    status: newStatus, viewsCount: l.ViewsCount, bookingsCount: l.BookingsCount,
                    images: l.Images);
                return Task.CompletedTask;
            })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { id, status = newStatus }, ct);
        if (env.Operation.Status != "Success")
            return this.ForbiddenEnvelope(env.Operation.FailedAnalyzer ?? "listing_toggle_failed",
                env.Operation.ErrorMessage);
        return this.OkEnvelope("listing.toggle", new { id, status = newStatus });
    }

    // ── Profile (GET + PUT) ────────────────────────────────────────────
    [HttpGet("/me/profile")]
    public IActionResult GetProfile()
    {
        var p = AshareV2Seed.Profile;
        return this.OkEnvelope("profile.get", new {
            id = p.Id, fullName = p.FullName,
            email = p.Email, emailVerified = p.EmailVerified,
            phone = p.Phone, phoneVerified = p.PhoneVerified,
            city = p.City, avatarUrl = p.AvatarUrl, memberSince = p.MemberSince
        });
    }

    public sealed record ProfileUpdateRequest(string FullName, string Email, string Phone, string City);
    [HttpPut("/me/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateRequest req, CancellationToken ct)
    {
        var op = Entry.Create("profile.update")
            .Describe("User updates own profile")
            .From(Caller, 1, ("role","user"))
            .To($"Profile:{AshareV2Seed.Profile.Id}", 1, ("role","updated"))
            .Tag("profile_id", AshareV2Seed.Profile.Id)
            .Analyze(new RequiredFieldAnalyzer("fullName", () => req.FullName))
            .Analyze(new RequiredFieldAnalyzer("phone",    () => req.Phone))
            .Analyze(new MaxLengthAnalyzer("fullName",     () => req.FullName, 100))
            .Execute(ctx =>
            {
                var old = AshareV2Seed.Profile;
                AshareV2Seed.Profile = old with {
                    FullName = req.FullName,
                    Email = req.Email,
                    EmailVerified = req.Email == old.Email && old.EmailVerified,
                    Phone = req.Phone,
                    PhoneVerified = req.Phone == old.Phone && old.PhoneVerified,
                    City = req.City
                };
                return Task.CompletedTask;
            })
            .Build();

        var profileData = new { id = AshareV2Seed.Profile.Id, fullName = req.FullName };
        var env = await _engine.ExecuteEnvelopeAsync(op, profileData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "profile_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("profile.update", profileData);
    }

    // ── MySubscription ─────────────────────────────────────────────────
    [HttpGet("/me/subscription")]
    public IActionResult GetSubscription()
    {
        var s = AshareV2Seed.ActiveSubscription;
        var plan = AshareV2Seed.Plans.FirstOrDefault(p => p.Id == s.PlanId);

        // الأرقام الفعليّة تُحسَب من الإعلانات المملوكة لا من بذرة ثابتة.
        var mine = AshareV2Seed.Listings.Where(l => l.OwnerId == CurrentUserId).ToList();
        var listingsUsed  = mine.Count(l => l.Status == 1);
        var featuredUsed  = mine.Count(l => l.Status == 1 && l.IsFeatured);

        return this.OkEnvelope("subscription.get", new {
            id = s.Id, planId = s.PlanId, planName = s.PlanName, status = s.Status,
            startDate = s.StartDate, endDate = s.EndDate,
            daysRemaining = (int)Math.Max(0, (s.EndDate - DateTime.UtcNow).TotalDays),
            listingsUsed, listingsLimit = s.ListingsLimit,
            featuredUsed, featuredLimit = s.FeaturedLimit,
            imagesPerListing = s.ImagesPerListing,
            apiCallsUsed = s.ApiCallsUsed, apiCallsLimit = s.ApiCallsLimit,
            features = plan?.Features ?? Array.Empty<string>()
        });
    }

    [HttpGet("/me/invoices")]
    public IActionResult Invoices() =>
        this.OkEnvelope("invoice.list",
            AshareV2Seed.Invoices.Select(i => new {
                id = i.Id, planId = i.PlanId, amount = i.Amount,
                date = i.Date, status = i.Status
            }));

    // ── Favorites ──────────────────────────────────────────────────────

    /// <summary>قائمة الإعلانات المفضّلة للمستخدم الحاليّ.</summary>
    [HttpGet("/favorites")]
    public IActionResult Favorites() =>
        this.OkEnvelope("favorite.list",
            AshareV2Seed.Listings
                .Where(l => AshareV2Seed.FavoriteIds.Contains(l.Id))
                .Select(l => new {
                    id = l.Id, title = l.Title, price = l.Price, timeUnit = l.TimeUnit,
                    city = l.City, district = l.District, categoryId = l.CategoryId,
                    rating = l.Rating, isFeatured = l.IsFeatured, status = l.Status
                }));

    /// <summary>
    /// تبديل حالة الإعلان في المفضّلة (إضافة ↔ حذف).
    /// لا قيد ملكيّة — يجوز تفضيل أيّ إعلان.
    /// </summary>
    [HttpPost("/listings/{id}/favorite")]
    public async Task<IActionResult> ToggleFavorite(string id, CancellationToken ct)
    {
        var listing = AshareV2Seed.Listings.FirstOrDefault(l => l.Id == id);
        if (listing is null) return this.NotFoundEnvelope("listing_not_found");

        bool adding = !AshareV2Seed.FavoriteIds.Contains(id);
        int sign = adding ? 1 : -1;

        var op = Entry.Create("listing.favorite.toggle")
            .Describe($"User {(adding ? "adds" : "removes")} listing {id} {(adding ? "to" : "from")} favorites")
            .From(Caller, sign, ("role", adding ? "liked" : "unliked"))
            .To($"Listing:{id}", sign, ("role", "favorited"))
            .Tag("listing_id", id)
            .Tag("action", adding ? "add" : "remove")
            .Execute(ctx =>
            {
                if (adding) AshareV2Seed.FavoriteIds.Add(id);
                else        AshareV2Seed.FavoriteIds.Remove(id);
                return Task.CompletedTask;
            })
            .Build();

        var favoriteData = new { id, isFavorite = adding };
        var env = await _engine.ExecuteEnvelopeAsync(op, favoriteData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "favorite_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("listing.favorite.toggle", favoriteData);
    }

    // ── Listing CRUD ───────────────────────────────────────────────────

    public sealed record CreateListingRequest(
        string Title, string Description,
        decimal Price, string TimeUnit,
        string City, string District,
        string CategoryId, int Capacity,
        double Lat, double Lng,
        IReadOnlyList<string>? Amenities);

    /// <summary>
    /// إنشاء إعلان جديد.
    /// السياسة: ListingQuotaInterceptor يفرض حدّ الخطّة عبر tag "quota_listing".
    /// </summary>
    [HttpPost("/my-listings")]
    public async Task<IActionResult> CreateListing([FromBody] CreateListingRequest req, CancellationToken ct)
    {
        var id = $"L-{Random.Shared.Next(10_000, 99_999)}";
        var listing = new AshareV2Seed.ListingSeed(
            id, req.Title, req.Description,
            req.Price, req.TimeUnit, req.City, req.District,
            req.Lat, req.Lng,
            req.Amenities ?? Array.Empty<string>(),
            ownerId: CurrentUserId, featured: false,
            capacity: req.Capacity, rating: 0m,
            categoryId: req.CategoryId, status: 1,
            viewsCount: 0, bookingsCount: 0);

        var op = Entry.Create("listing.create")
            .Describe($"Owner creates listing: {req.Title}")
            .From(Caller, 1, ("role", "owner"))
            .To($"Listing:{id}", 1, ("role", "created"))
            .Tag("listing_id", id)
            .Tag("quota_listing", "true")
            .Analyze(new RequiredFieldAnalyzer("title",       () => req.Title))
            .Analyze(new RequiredFieldAnalyzer("city",        () => req.City))
            .Analyze(new MaxLengthAnalyzer("title",           () => req.Title, 200))
            .Analyze(new MaxLengthAnalyzer("description",     () => req.Description, 2000))
            .Analyze(new RangeAnalyzer("price",               () => (int)req.Price, 1, 1_000_000))
            .Analyze(new RangeAnalyzer("capacity",            () => req.Capacity, 1, 50))
            .Execute(ctx =>
            {
                AshareV2Seed.Listings.Add(listing);
                return Task.CompletedTask;
            })
            .Build();

        var listingData = new { id, title = listing.Title, status = listing.Status };
        var env = await _engine.ExecuteEnvelopeAsync(op, listingData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "listing_create_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("listing.create", listingData);
    }

    public sealed record UpdateListingRequest(
        string? Title, string? Description,
        decimal? Price, string? TimeUnit,
        string? City, string? District,
        string? CategoryId, int? Capacity,
        double? Lat, double? Lng,
        IReadOnlyList<string>? Amenities);

    /// <summary>
    /// تعديل إعلان موجود.
    /// السياسة: OwnershipInterceptor يفرض must_own.
    /// </summary>
    [HttpPut("/my-listings/{id}")]
    public async Task<IActionResult> UpdateListing(string id, [FromBody] UpdateListingRequest req, CancellationToken ct)
    {
        var ix = AshareV2Seed.Listings.FindIndex(l => l.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("listing_not_found");
        var l = AshareV2Seed.Listings[ix];

        var op = Entry.Create("listing.update")
            .Describe($"Owner updates listing {id}")
            .From(Caller, 1, ("role", "owner"))
            .To($"Listing:{id}", 1, ("role", "updated"))
            .Tag("listing_id", id)
            .Tag("owner_policy", "must_own")
            .Tag("resource_owner", l.OwnerId)
            .Analyze(new MaxLengthAnalyzer("title",       () => req.Title ?? l.Title, 200))
            .Analyze(new MaxLengthAnalyzer("description", () => req.Description ?? l.Description, 2000))
            .Execute(ctx =>
            {
                AshareV2Seed.Listings[ix] = new AshareV2Seed.ListingSeed(
                    id,
                    req.Title       ?? l.Title,
                    req.Description ?? l.Description,
                    req.Price       ?? l.Price,
                    req.TimeUnit    ?? l.TimeUnit,
                    req.City        ?? l.City,
                    req.District    ?? l.District,
                    req.Lat         ?? l.Lat,
                    req.Lng         ?? l.Lng,
                    req.Amenities   ?? l.Amenities,
                    ownerId: l.OwnerId, featured: l.IsFeatured,
                    capacity:    req.Capacity   ?? l.Capacity,
                    rating:      l.Rating,
                    categoryId:  req.CategoryId ?? l.CategoryId,
                    status:      l.Status,
                    viewsCount:  l.ViewsCount,
                    bookingsCount: l.BookingsCount,
                    images: l.Images);
                return Task.CompletedTask;
            })
            .Build();

        var updateData = new { id, title = req.Title ?? l.Title };
        var env = await _engine.ExecuteEnvelopeAsync(op, updateData, ct);
        if (env.Operation.Status != "Success")
            return this.ForbiddenEnvelope(env.Operation.FailedAnalyzer ?? "listing_update_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("listing.update", updateData);
    }

    /// <summary>
    /// حذف إعلان.
    /// السياسة: OwnershipInterceptor يفرض must_own.
    /// </summary>
    [HttpDelete("/my-listings/{id}")]
    public async Task<IActionResult> DeleteListing(string id, CancellationToken ct)
    {
        var ix = AshareV2Seed.Listings.FindIndex(l => l.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("listing_not_found");
        var l = AshareV2Seed.Listings[ix];

        var op = Entry.Create("listing.delete")
            .Describe($"Owner deletes listing {id}")
            .From(Caller, 1, ("role", "owner"))
            .To($"Listing:{id}", -1, ("role", "deleted"))
            .Tag("listing_id", id)
            .Tag("owner_policy", "must_own")
            .Tag("resource_owner", l.OwnerId)
            .Execute(ctx =>
            {
                AshareV2Seed.Listings.RemoveAt(ix);
                return Task.CompletedTask;
            })
            .Build();

        var deleteData = new { id, deleted = true };
        var env = await _engine.ExecuteEnvelopeAsync(op, deleteData, ct);
        if (env.Operation.Status != "Success")
            return this.ForbiddenEnvelope(env.Operation.FailedAnalyzer ?? "listing_delete_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("listing.delete", deleteData);
    }

    // ── Reviews ────────────────────────────────────────────────────────

    /// <summary>تقييمات إعلان محدّد.</summary>
    [HttpGet("/listings/{id}/reviews")]
    public IActionResult GetReviews(string id)
    {
        if (!AshareV2Seed.Listings.Any(l => l.Id == id))
            return this.NotFoundEnvelope("listing_not_found");
        return this.OkEnvelope("listing.reviews",
            AshareV2Seed.Reviews
                .Where(r => r.ListingId == id)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new {
                    id = r.Id, rating = r.Rating, comment = r.Comment,
                    authorName = r.AuthorName, createdAt = r.CreatedAt
                }));
    }

    public sealed record CreateReviewRequest(int Rating, string? Comment);

    /// <summary>
    /// إضافة تقييم على حجز مكتمل.
    /// السياسة: owner_policy = must_own (المستخدم يجب أن يكون صاحب الحجز).
    /// شرط: الحجز يجب أن يكون "completed"، ولا يمكن تقييم نفس الحجز مرّتين.
    /// </summary>
    [HttpPost("/bookings/{id}/review")]
    public async Task<IActionResult> CreateReview(string id, [FromBody] CreateReviewRequest req, CancellationToken ct)
    {
        var booking = AshareV2Seed.Bookings.FirstOrDefault(b => b.Id == id);
        if (booking is null) return this.NotFoundEnvelope("booking_not_found");
        if (booking.Status != "completed")
            return this.BadRequestEnvelope("booking_not_completed", "لا يمكن تقييم حجز غير مكتمل");
        if (AshareV2Seed.Reviews.Any(r => r.BookingId == id))
            return this.BadRequestEnvelope("already_reviewed", "هذا الحجز قُيِّم مسبقاً");

        var reviewId = $"RV-{Random.Shared.Next(10_000, 99_999)}";
        var review = new AshareV2Seed.ReviewSeed(
            reviewId, booking.ListingId, booking.Id, CurrentUserId,
            AshareV2Seed.Profile.FullName, req.Rating, req.Comment ?? string.Empty,
            DateTime.UtcNow);

        var op = Entry.Create("booking.review.create")
            .Describe($"User reviews booking {id} — {req.Rating} stars")
            .From(Caller, 1, ("role", "reviewer"))
            .To($"Listing:{booking.ListingId}", 1, ("role", "reviewed"))
            .Tag("booking_id",  id)
            .Tag("listing_id",  booking.ListingId)
            .Tag("owner_policy",    "must_own")
            .Tag("resource_owner",  CurrentUserId)
            .Analyze(new RangeAnalyzer("rating", () => req.Rating, 1, 5))
            .Analyze(new MaxLengthAnalyzer("comment", () => req.Comment, 1000))
            .Execute(ctx =>
            {
                AshareV2Seed.Reviews.Add(review);
                return Task.CompletedTask;
            })
            .Build();

        var reviewData = new { id = reviewId, listingId = booking.ListingId, rating = req.Rating };
        var env = await _engine.ExecuteEnvelopeAsync(op, reviewData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "review_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("booking.review.create", reviewData);
    }

    // ── Booking cancel ─────────────────────────────────────────────────

    /// <summary>
    /// إلغاء حجز.
    /// السياسة: owner_policy = must_own (المستخدم يجب أن يكون صاحب الحجز).
    /// </summary>
    [HttpPost("/bookings/{id}/cancel")]
    public async Task<IActionResult> CancelBooking(string id, CancellationToken ct)
    {
        var ix = AshareV2Seed.Bookings.FindIndex(b => b.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("booking_not_found");
        var b = AshareV2Seed.Bookings[ix];

        if (b.Status == "cancelled")
            return this.BadRequestEnvelope("already_cancelled", "الحجز ملغى مسبقاً");
        if (b.Status == "completed")
            return this.BadRequestEnvelope("booking_completed", "لا يمكن إلغاء حجز مكتمل");

        var op = Entry.Create("booking.cancel")
            .Describe($"User cancels booking {id}")
            .From(Caller, -1, ("role", "canceller"))
            .To($"Booking:{id}", -1, ("role", "cancelled"))
            .Tag("booking_id",   id)
            .Tag("listing_id",   b.ListingId)
            .Tag("owner_policy", "must_own")
            .Tag("resource_owner", b.UserId)
            .Execute(ctx =>
            {
                AshareV2Seed.Bookings[ix] = b with { Status = "cancelled" };
                return Task.CompletedTask;
            })
            .Build();

        var cancelData = new { id, status = "cancelled" };
        var env = await _engine.ExecuteEnvelopeAsync(op, cancelData, ct);
        if (env.Operation.Status != "Success")
            return this.ForbiddenEnvelope(env.Operation.FailedAnalyzer ?? "cancel_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("booking.cancel", cancelData);
    }
}
