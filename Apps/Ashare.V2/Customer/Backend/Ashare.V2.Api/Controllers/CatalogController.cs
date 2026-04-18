using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
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
    public CatalogController(OpEngine engine) => _engine = engine;

    // Complaints replies live here in-memory so POST creates are visible immediately.
    private static readonly List<AshareV2Seed.ComplaintSeed> _complaintsMutable =
        AshareV2Seed.Complaints.ToList();

    private string Caller => $"User:{AshareV2Seed.CurrentUserId}";

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
            .Execute(ctx => Task.CompletedTask)   // in-memory: مجرّد الإعلان يكفي للعرض
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, payload, ct);
        if (env.Operation.Status != "Success")
            return this.ForbiddenEnvelope(env.Operation.FailedAnalyzer ?? "booking_failed",
                env.Operation.ErrorMessage);
        return Ok(env);
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
            Id:   $"m-{conv.Messages.Count + 1}",
            From: "me",
            Text: req.Text ?? string.Empty,
            SentAt: DateTime.UtcNow);

        var op = Entry.Create("message.send")
            .Describe($"User sends message to conversation {id}")
            .From(Caller, 1, ("role","sender"))
            .To($"Conversation:{id}", 1, ("role","appended"))
            .Tag("conversation_id", id)
            .Execute(ctx =>
            {
                conv.Messages.Add(msg);
                AshareV2Seed.Conversations[ix] = conv with { LastAt = msg.SentAt, UnreadCount = 0 };
                return Task.CompletedTask;
            })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op,
            new { id = msg.Id, from = msg.From, text = msg.Text, sentAt = msg.SentAt }, ct);
        return env.Operation.Status == "Success"
            ? Ok(env)
            : this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "send_failed", env.Operation.ErrorMessage);
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
        return Ok(env);
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
            .Execute(ctx => { _complaintsMutable.Insert(0, c); return Task.CompletedTask; })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op,
            new { id = c.Id, subject = c.Subject, status = c.Status, createdAt = c.CreatedAt }, ct);
        return env.Operation.Status == "Success"
            ? Ok(env)
            : this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "complaint_failed", env.Operation.ErrorMessage);
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
            .Execute(ctx =>
            {
                var c = _complaintsMutable[ix];
                var newReplies = c.Replies.Append(new AshareV2Seed.ComplaintReplySeed(
                    $"R{c.Replies.Count + 1}", "user", req.Message, DateTime.UtcNow)).ToList();
                _complaintsMutable[ix] = c with { Replies = newReplies };
                return Task.CompletedTask;
            })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op,
            new { id, repliesCount = _complaintsMutable[ix].Replies.Count + 1 }, ct);
        return env.Operation.Status == "Success"
            ? Ok(env)
            : this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "reply_failed", env.Operation.ErrorMessage);
    }

    [HttpGet("/my-listings")]
    public IActionResult MyListings() =>
        this.OkEnvelope("listing.my",
            AshareV2Seed.Listings
                .Where(l => l.OwnerId == AshareV2Seed.CurrentUserId)
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
                    status: newStatus, viewsCount: l.ViewsCount, bookingsCount: l.BookingsCount);
                return Task.CompletedTask;
            })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { id, status = newStatus }, ct);
        if (env.Operation.Status != "Success")
            return this.ForbiddenEnvelope(env.Operation.FailedAnalyzer ?? "listing_toggle_failed",
                env.Operation.ErrorMessage);
        return Ok(env);
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

        var env = await _engine.ExecuteEnvelopeAsync(op,
            new { id = AshareV2Seed.Profile.Id, fullName = req.FullName }, ct);
        return env.Operation.Status == "Success"
            ? Ok(env)
            : this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "profile_failed", env.Operation.ErrorMessage);
    }

    // ── MySubscription ─────────────────────────────────────────────────
    [HttpGet("/me/subscription")]
    public IActionResult GetSubscription()
    {
        var s = AshareV2Seed.ActiveSubscription;
        var plan = AshareV2Seed.Plans.FirstOrDefault(p => p.Id == s.PlanId);

        // الأرقام الفعليّة تُحسَب من الإعلانات المملوكة لا من بذرة ثابتة.
        var mine = AshareV2Seed.Listings.Where(l => l.OwnerId == AshareV2Seed.CurrentUserId).ToList();
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
}
