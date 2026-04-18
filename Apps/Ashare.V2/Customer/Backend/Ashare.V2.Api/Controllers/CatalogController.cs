using ACommerce.OperationEngine.Wire.Http;
using Ashare.V2.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.V2.Api.Controllers;

/// <summary>
/// Endpoints إضافيّة لـ Ashare.V2: cities, plans, legal, version, profile,
/// subscription, complaints thread.
/// </summary>
[ApiController]
public class CatalogController : ControllerBase
{
    // Complaints replies live here in-memory so POST creates are visible immediately.
    private static readonly List<AshareV2Seed.ComplaintSeed> _complaintsMutable =
        AshareV2Seed.Complaints.ToList();

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
    /// <summary>إنشاء حجز — يمنع حجز إعلان مملوك للمستخدم الحاليّ.</summary>
    [HttpPost("/bookings")]
    public IActionResult CreateBooking([FromBody] CreateBookingRequest req)
    {
        var listing = AshareV2Seed.Listings.FirstOrDefault(l => l.Id == req.ListingId);
        if (listing is null) return this.NotFoundEnvelope("listing_not_found");
        if (listing.OwnerId == AshareV2Seed.CurrentUserId)
            return this.ForbiddenEnvelope("self_booking", "لا يمكنك حجز إعلان من إنشائك");
        if (listing.Status != 1)
            return this.BadRequestEnvelope("listing_inactive", "الإعلان غير نشط حاليّاً");

        var total = listing.Price * req.Nights;
        return this.OkEnvelope("booking.create", new {
            id = $"B-{Random.Shared.Next(10_000, 99_999)}",
            listingId = req.ListingId,
            listingTitle = listing.Title,
            total, startDate = req.StartDate, nights = req.Nights, guests = req.Guests,
            status = "pending"
        });
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
    public IActionResult SendMessage(string id, [FromBody] SendMessageRequest req)
    {
        var ix = AshareV2Seed.Conversations.FindIndex(c => c.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("conversation_not_found");
        var conv = AshareV2Seed.Conversations[ix];
        var msg  = new AshareV2Seed.MessageSeed(
            Id:   $"m-{conv.Messages.Count + 1}",
            From: "me",
            Text: req.Text ?? string.Empty,
            SentAt: DateTime.UtcNow);
        conv.Messages.Add(msg);
        AshareV2Seed.Conversations[ix] = conv with { LastAt = msg.SentAt, UnreadCount = 0 };
        return this.OkEnvelope("conversation.send", new {
            id = msg.Id, from = msg.From, text = msg.Text, sentAt = msg.SentAt
        });
    }

    public sealed record StartConversationRequest(string ListingId, string Text);
    /// <summary>فتح محادثة مع مالك إعلان (يمنع محادثة النفس).</summary>
    [HttpPost("/conversations/start")]
    public IActionResult StartConversation([FromBody] StartConversationRequest req)
    {
        var listing = AshareV2Seed.Listings.FirstOrDefault(l => l.Id == req.ListingId);
        if (listing is null) return this.NotFoundEnvelope("listing_not_found");
        if (listing.OwnerId == AshareV2Seed.CurrentUserId)
            return this.ForbiddenEnvelope("self_message",
                "لا يمكن مراسلة إعلان من إنشائك");

        // إن كانت المحادثة موجودة مع نفس المالك، استخدمها.
        var existing = AshareV2Seed.Conversations.FirstOrDefault(c =>
            c.PartnerId == listing.OwnerId && c.ListingId == listing.Id);
        if (existing is not null)
        {
            return this.OkEnvelope("conversation.start", new {
                id = existing.Id, created = false
            });
        }

        var id = $"C-{AshareV2Seed.Conversations.Count + 1}";
        var conv = new AshareV2Seed.ConversationSeed(
            id, "مالك " + listing.Title, listing.Title, DateTime.UtcNow, 0,
            partnerId: listing.OwnerId, listingId: listing.Id,
            messages: new List<AshareV2Seed.MessageSeed>
            {
                new("m-1", "me", req.Text, DateTime.UtcNow)
            });
        AshareV2Seed.Conversations.Add(conv);
        return this.OkEnvelope("conversation.start", new { id, created = true });
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
    public IActionResult CreateComplaint([FromBody] CreateComplaintRequest req)
    {
        var id = $"X-{_complaintsMutable.Count + 1:D3}";
        var c = new AshareV2Seed.ComplaintSeed(
            id, req.Subject, req.Body, DateTime.UtcNow, "open",
            req.Priority ?? "عادي", req.RelatedEntity ?? "",
            new List<AshareV2Seed.ComplaintReplySeed> {
                new("R1", "user", req.Body, DateTime.UtcNow)
            });
        _complaintsMutable.Insert(0, c);
        return this.OkEnvelope("complaint.file", new {
            id = c.Id, subject = c.Subject, status = c.Status, createdAt = c.CreatedAt
        });
    }

    public sealed record ReplyRequest(string Message);
    [HttpPost("/complaints/{id}/replies")]
    public IActionResult AddReply(string id, [FromBody] ReplyRequest req)
    {
        var ix = _complaintsMutable.FindIndex(x => x.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("complaint_not_found");
        var c = _complaintsMutable[ix];
        var newReplies = c.Replies.Append(new AshareV2Seed.ComplaintReplySeed(
            $"R{c.Replies.Count + 1}", "user", req.Message, DateTime.UtcNow)).ToList();
        _complaintsMutable[ix] = c with { Replies = newReplies };
        return this.OkEnvelope("complaint.reply", new {
            id = c.Id, repliesCount = newReplies.Count
        });
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

    /// <summary>تبديل حالة إعلان نشط/موقوف (ownership-enforced).</summary>
    [HttpPost("/my-listings/{id}/toggle")]
    public IActionResult ToggleListing(string id)
    {
        var ix = AshareV2Seed.Listings.FindIndex(l => l.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("listing_not_found");
        var l = AshareV2Seed.Listings[ix];
        if (l.OwnerId != AshareV2Seed.CurrentUserId)
            return this.ForbiddenEnvelope("not_owner", "لا يمكن تعديل إعلان غير مملوك لك");
        // نسخ الـ record مع تبديل الحالة
        var newStatus = l.Status == 1 ? 2 : 1;
        AshareV2Seed.Listings[ix] = new AshareV2Seed.ListingSeed(
            l.Id, l.Title, l.Description, l.Price, l.TimeUnit, l.City, l.District,
            l.Lat, l.Lng, l.Amenities,
            ownerId: l.OwnerId, featured: l.IsFeatured, capacity: l.Capacity,
            rating: l.Rating, categoryId: l.CategoryId,
            status: newStatus, viewsCount: l.ViewsCount, bookingsCount: l.BookingsCount);
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
    public IActionResult UpdateProfile([FromBody] ProfileUpdateRequest req)
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
        return this.OkEnvelope("profile.update", new {
            id = AshareV2Seed.Profile.Id, fullName = AshareV2Seed.Profile.FullName
        });
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
