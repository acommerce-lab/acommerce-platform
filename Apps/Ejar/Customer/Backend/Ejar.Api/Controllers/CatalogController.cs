using ACommerce.Chat.Operations;
using ACommerce.Notification.Providers.InApp;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;
using ACommerce.OperationEngine.Wire;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.Realtime.Operations;
using ACommerce.Realtime.Operations.Abstractions;
using Ejar.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ejar.Api.Controllers;

/// <summary>
/// نقاط نهاية تتطلب مصادقة:
/// الملف الشخصي، الإعلانات، المحادثات، الإشعارات، الشكاوى، المفضلة، الباقات.
/// </summary>
[ApiController]
[Authorize]
public class CatalogController : ControllerBase
{
    private readonly OpEngine _engine;
    private readonly IChatService? _chat;
    private readonly IConnectionTracker? _connections;
    private readonly InAppNotificationChannel? _inApp;
    private static readonly List<EjarSeed.ComplaintSeed> _complaints =
        EjarSeed.Complaints.ToList();

    /// <summary>
    /// Idle timeout for chat channels — after this many seconds without a sent or
    /// received message, the backend auto-closes the user's chat channel and
    /// re-opens the notification channel. Apps can wire this to config later.
    /// </summary>
    private static readonly TimeSpan ChatIdleTimeout = TimeSpan.FromMinutes(2);

    public CatalogController(
        OpEngine engine,
        IChatService? chat = null,
        IConnectionTracker? connections = null,
        InAppNotificationChannel? inApp = null)
    {
        _engine      = engine;
        _chat        = chat;
        _connections = connections;
        _inApp       = inApp;
    }

    private string CurrentUserId =>
        User.FindFirstValue("user_id") ?? EjarSeed.CurrentUserId;

    // ═══════════════════════════════════════════════════════════════════════
    // Profile
    // ═══════════════════════════════════════════════════════════════════════

    [HttpGet("/me/profile")]
    public IActionResult GetProfile()
    {
        var u = EjarSeed.GetUser(CurrentUserId);
        if (u is null) return this.NotFoundEnvelope("user_not_found");
        return this.OkEnvelope("profile.get", new {
            id = u.Id, fullName = u.FullName,
            phone = u.Phone, phoneVerified = u.PhoneVerified,
            email = u.Email, emailVerified = u.EmailVerified,
            city = u.City, memberSince = u.MemberSince
        });
    }

    public sealed record ProfileUpdateRequest(string FullName, string Email, string Phone, string City);

    [HttpPut("/me/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateRequest req, CancellationToken ct)
    {
        var userId = CurrentUserId;
        var op = Entry.Create("profile.update")
            .Describe("User updates own profile")
            .From($"User:{userId}", 1, ("role", "user"))
            .To($"Profile:{userId}", 1, ("role", "updated"))
            .Tag("user_id", userId)
            .Analyze(new RequiredFieldAnalyzer("fullName", () => req.FullName))
            .Analyze(new MaxLengthAnalyzer("fullName", () => req.FullName, 100))
            .Execute(_ =>
            {
                EjarSeed.UpdateUser(userId, req.FullName, req.Email ?? "", req.Phone ?? "", req.City ?? "");
                return Task.CompletedTask;
            })
            .Build();

        var data = new { id = userId, fullName = req.FullName };
        var env  = await _engine.ExecuteEnvelopeAsync(op, data, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "profile_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("profile.update", data);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // My Listings
    // ═══════════════════════════════════════════════════════════════════════

    [HttpGet("/my-listings")]
    public IActionResult MyListings() =>
        this.OkEnvelope("listing.my",
            EjarSeed.Listings
                .Where(l => l.OwnerId == CurrentUserId)
                .Select(l => new {
                    id = l.Id, title = l.Title, price = l.Price,
                    timeUnit = l.TimeUnit, propertyType = l.PropertyType,
                    city = l.City, district = l.District,
                    status = l.Status, viewsCount = l.ViewsCount, isVerified = l.IsVerified
                }));

    [HttpPost("/my-listings/{id}/toggle")]
    public async Task<IActionResult> ToggleListing(string id, CancellationToken ct)
    {
        var ix = EjarSeed.Listings.FindIndex(l => l.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("listing_not_found");
        var l = EjarSeed.Listings[ix];
        if (l.OwnerId != CurrentUserId)
            return this.ForbiddenEnvelope("not_owner", "ليس لديك صلاحية تعديل هذا الإعلان");

        var newStatus = l.Status == 1 ? 2 : 1;

        var op = Entry.Create("listing.toggle")
            .Describe($"Owner toggles listing {id} → status {newStatus}")
            .From($"User:{CurrentUserId}", 1, ("role", "owner"))
            .To($"Listing:{id}", 1, ("role", "target"))
            .Tag("listing_id", id)
            .Execute(ctx =>
            {
                EjarSeed.Listings[ix] = l with { Status = newStatus };
                return Task.CompletedTask;
            })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { id, status = newStatus }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "toggle_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("listing.toggle", new { id, status = newStatus });
    }

    public sealed record CreateListingRequest(
        string Title, string Description,
        decimal Price, string TimeUnit, string PropertyType,
        string City, string District,
        double Lat, double Lng,
        int BedroomCount, int BathroomCount, int AreaSqm,
        IReadOnlyList<string>? Amenities);

    [HttpPost("/my-listings")]
    public async Task<IActionResult> CreateListing([FromBody] CreateListingRequest req, CancellationToken ct)
    {
        var id      = $"L-{Random.Shared.Next(10_000, 99_999)}";
        var userId  = CurrentUserId;
        var listing = new EjarSeed.ListingSeed(
            id, req.Title, req.Description,
            req.Price, req.TimeUnit, req.PropertyType,
            req.City, req.District, req.Lat, req.Lng,
            req.Amenities ?? Array.Empty<string>(),
            userId,
            req.BedroomCount, req.BathroomCount, req.AreaSqm);

        var op = Entry.Create("listing.create")
            .Describe($"Owner creates listing: {req.Title}")
            .From($"User:{userId}", 1, ("role", "owner"))
            .To($"Listing:{id}", 1, ("role", "created"))
            .Tag("listing_id", id)
            .Analyze(new RequiredFieldAnalyzer("title",    () => req.Title))
            .Analyze(new RequiredFieldAnalyzer("city",     () => req.City))
            .Analyze(new MaxLengthAnalyzer("title",        () => req.Title, 200))
            .Analyze(new MaxLengthAnalyzer("description",  () => req.Description, 3000))
            .Analyze(new RangeAnalyzer("price",            () => (int)req.Price, 1, 10_000_000))
            .Execute(ctx => { EjarSeed.Listings.Add(listing); return Task.CompletedTask; })
            .Build();

        var data = new { id, title = listing.Title, status = listing.Status };
        var env  = await _engine.ExecuteEnvelopeAsync(op, data, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "listing_create_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("listing.create", data);
    }

    [HttpDelete("/my-listings/{id}")]
    public async Task<IActionResult> DeleteListing(string id, CancellationToken ct)
    {
        var ix = EjarSeed.Listings.FindIndex(l => l.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("listing_not_found");
        if (EjarSeed.Listings[ix].OwnerId != CurrentUserId)
            return this.ForbiddenEnvelope("not_owner", "ليس لديك صلاحية حذف هذا الإعلان");

        var op = Entry.Create("listing.delete")
            .Describe($"Owner deletes listing {id}")
            .From($"User:{CurrentUserId}", 1, ("role", "owner"))
            .To($"Listing:{id}", -1, ("role", "deleted"))
            .Tag("listing_id", id)
            .Execute(ctx => { EjarSeed.Listings.RemoveAt(ix); return Task.CompletedTask; })
            .Build();

        var deleteData = new { id, deleted = true };
        var env = await _engine.ExecuteEnvelopeAsync(op, deleteData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "listing_delete_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("listing.delete", deleteData);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Favorites
    // ═══════════════════════════════════════════════════════════════════════

    [HttpGet("/favorites")]
    public IActionResult Favorites() =>
        this.OkEnvelope("favorite.list",
            EjarSeed.Listings
                .Where(l => EjarSeed.FavoriteIds.Contains(l.Id))
                .Select(l => new {
                    id = l.Id, title = l.Title, price = l.Price,
                    timeUnit = l.TimeUnit, propertyType = l.PropertyType,
                    city = l.City, district = l.District, isVerified = l.IsVerified
                }));

    [HttpPost("/listings/{id}/favorite")]
    public async Task<IActionResult> ToggleFavorite(string id, CancellationToken ct)
    {
        var listing = EjarSeed.Listings.FirstOrDefault(l => l.Id == id);
        if (listing is null) return this.NotFoundEnvelope("listing_not_found");

        var adding = !EjarSeed.FavoriteIds.Contains(id);
        var sign   = adding ? 1 : -1;

        var op = Entry.Create("listing.favorite.toggle")
            .Describe($"User {(adding ? "favorites" : "unfavorites")} listing {id}")
            .From($"User:{CurrentUserId}", sign, ("role", adding ? "liked" : "unliked"))
            .To($"Listing:{id}", sign, ("role", "favorited"))
            .Tag("listing_id", id)
            .Tag("action", adding ? "add" : "remove")
            .Execute(ctx =>
            {
                if (adding) EjarSeed.FavoriteIds.Add(id);
                else        EjarSeed.FavoriteIds.Remove(id);
                return Task.CompletedTask;
            })
            .Build();

        var favoriteData = new { id, isFavorite = adding };
        var env = await _engine.ExecuteEnvelopeAsync(op, favoriteData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "favorite_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("listing.favorite.toggle", favoriteData);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Conversations
    // ═══════════════════════════════════════════════════════════════════════

    [HttpGet("/conversations")]
    public IActionResult Conversations() =>
        this.OkEnvelope("conversation.list",
            EjarSeed.Conversations.Select(c => new {
                id = c.Id, partnerName = c.PartnerName, partnerId = c.PartnerId,
                listingId = c.ListingId, subject = c.Subject,
                lastAt = c.LastAt, unreadCount = c.UnreadCount,
                lastMessage = c.Messages.LastOrDefault()?.Text ?? ""
            }));

    [HttpGet("/conversations/{id}")]
    public IActionResult ConversationDetails(string id)
    {
        var c = EjarSeed.Conversations.FirstOrDefault(x => x.Id == id);
        if (c is null) return this.NotFoundEnvelope("conversation_not_found");
        return this.OkEnvelope("conversation.details", new {
            id = c.Id, partnerName = c.PartnerName, partnerId = c.PartnerId,
            listingId = c.ListingId, subject = c.Subject,
            messages = c.Messages.Select(m => new {
                id = m.Id, from = m.From, text = m.Text, sentAt = m.SentAt
            })
        });
    }

    public sealed record StartConversationRequest(string ListingId, string Text);

    [HttpPost("/conversations/start")]
    public async Task<IActionResult> StartConversation([FromBody] StartConversationRequest req, CancellationToken ct)
    {
        var listing = EjarSeed.Listings.FirstOrDefault(l => l.Id == req.ListingId);
        if (listing is null) return this.NotFoundEnvelope("listing_not_found");

        var existing = EjarSeed.Conversations.FirstOrDefault(c =>
            c.ListingId == req.ListingId && c.PartnerId == listing.OwnerId);
        var newId = existing?.Id ?? $"C-{EjarSeed.Conversations.Count + 1}";

        var op = Entry.Create("conversation.start")
            .Describe($"User opens chat on listing {listing.Id}")
            .From($"User:{CurrentUserId}", 1, ("role", "initiator"))
            .To($"Listing:{listing.Id}", 1, ("role", "subject"))
            .Tag("listing_id", listing.Id)
            .Execute(ctx =>
            {
                if (existing is not null) return Task.CompletedTask;
                var conv = new EjarSeed.ConversationSeed(
                    newId, "المؤجر", listing.OwnerId, listing.Id, listing.Title,
                    DateTime.UtcNow, 0,
                    new List<EjarSeed.MessageSeed> {
                        new("M-auto", "me", req.Text, DateTime.UtcNow)
                    });
                EjarSeed.Conversations.Add(conv);
                return Task.CompletedTask;
            })
            .Build();

        var data = new { id = newId, created = existing is null };
        var env  = await _engine.ExecuteEnvelopeAsync(op, data, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "conversation_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("conversation.start", data);
    }

    public sealed record SendMessageRequest(string Text);

    [HttpPost("/conversations/{id}/messages")]
    public async Task<IActionResult> SendMessage(string id, [FromBody] SendMessageRequest req, CancellationToken ct)
    {
        var ix = EjarSeed.Conversations.FindIndex(c => c.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("conversation_not_found");
        var conv = EjarSeed.Conversations[ix];
        var msg  = new EjarSeed.MessageSeed(
            $"M-{conv.Messages.Count + 1}", id, "me",
            req.Text ?? string.Empty, DateTime.UtcNow);

        var op = Entry.Create("message.send")
            .Describe($"User sends message to conversation {id}")
            .From($"User:{CurrentUserId}", 1, ("role", "sender"))
            .To($"Conversation:{id}", 1, ("role", "appended"))
            .Tag("conversation_id", id)
            .Analyze(new RequiredFieldAnalyzer("text", () => req.Text))
            .Analyze(new MaxLengthAnalyzer("text",    () => req.Text, 4000))
            .Execute(ctx =>
            {
                conv.Messages.Add(msg);
                EjarSeed.Conversations[ix] = conv with { LastAt = msg.SentAt, UnreadCount = 0 };
                return Task.CompletedTask;
            })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, (IChatMessage)msg, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "send_failed",
                                           env.Operation.ErrorMessage);

        // Broadcast on BOTH chat:conv:X and notif:conv:X — each subscriber
        // (partner) receives whichever they're currently subscribed to:
        // chat group if their ChatRoom page is open, notif group otherwise.
        // The chat lib decides nothing about per-recipient suppression.
        if (_chat is not null) await _chat.BroadcastNewMessageAsync(msg, CancellationToken.None);

        return this.OkEnvelope("message.send", msg);
    }

    // ─── Chat channel lifecycle (frontend ChatRoom calls these) ─────────────────

    [HttpPost("/chat/{convId}/enter")]
    public async Task<IActionResult> EnterChat(string convId, CancellationToken ct)
    {
        if (_chat is null) return this.OkEnvelope("chat.enter", new { ok = true }); // no-op when disabled
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

    // ═══════════════════════════════════════════════════════════════════════
    // Notifications
    // ═══════════════════════════════════════════════════════════════════════

    [HttpGet("/notifications")]
    public IActionResult Notifications() =>
        this.OkEnvelope("notification.list",
            EjarSeed.Notifications.Select(n => new {
                id = n.Id, title = n.Title, body = n.Body, type = n.Type,
                createdAt = n.CreatedAt, isRead = n.IsRead, relatedId = n.RelatedId
            }));

    [HttpPost("/notifications/{id}/read")]
    public async Task<IActionResult> ReadNotification(string id, CancellationToken ct)
    {
        var ix = EjarSeed.Notifications.FindIndex(n => n.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("notification_not_found");

        var op = Entry.Create("notification.read")
            .Describe($"Mark notification {id} read")
            .From($"User:{CurrentUserId}", 1, ("role", "reader"))
            .To($"Notification:{id}", 1, ("role", "read"))
            .Tag("notification_id", id)
            .Execute(ctx =>
            {
                var n = EjarSeed.Notifications[ix];
                EjarSeed.Notifications[ix] = n with { IsRead = true };
                return Task.CompletedTask;
            })
            .Build();

        var data = new { id, isRead = true };
        var env  = await _engine.ExecuteEnvelopeAsync(op, data, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope("notification_read_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("notification.read", data);
    }

    [HttpPost("/notifications/read-all")]
    public async Task<IActionResult> ReadAllNotifications(CancellationToken ct)
    {
        var op = Entry.Create("notification.read.all")
            .Describe("Mark all notifications read")
            .From($"User:{CurrentUserId}", 1, ("role", "reader"))
            .To("System:notifications", 1, ("role", "batch"))
            .Execute(ctx =>
            {
                for (var i = 0; i < EjarSeed.Notifications.Count; i++)
                    EjarSeed.Notifications[i] = EjarSeed.Notifications[i] with { IsRead = true };
                return Task.CompletedTask;
            })
            .Build();

        var env = await _engine.ExecuteEnvelopeAsync(op, new { count = EjarSeed.Notifications.Count }, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope("read_all_failed", env.Operation.ErrorMessage);
        return this.OkEnvelope("notification.read.all", new { count = EjarSeed.Notifications.Count });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Complaints
    // ═══════════════════════════════════════════════════════════════════════

    [HttpGet("/complaints")]
    public IActionResult Complaints() =>
        this.OkEnvelope("complaint.list",
            _complaints.Select(c => new {
                id = c.Id, subject = c.Subject, body = c.Body,
                createdAt = c.CreatedAt, status = c.Status,
                priority = c.Priority, relatedEntity = c.RelatedEntity,
                repliesCount = c.Replies.Count
            }));

    [HttpGet("/complaints/{id}")]
    public IActionResult ComplaintDetails(string id)
    {
        var c = _complaints.FirstOrDefault(x => x.Id == id);
        if (c is null) return this.NotFoundEnvelope("complaint_not_found");
        return this.OkEnvelope("complaint.details", new {
            id = c.Id, subject = c.Subject, body = c.Body,
            createdAt = c.CreatedAt, status = c.Status, priority = c.Priority,
            replies = c.Replies.Select(r => new {
                id = r.Id, from = r.From, message = r.Message, createdAt = r.CreatedAt
            })
        });
    }

    public sealed record CreateComplaintRequest(string Subject, string Body, string? Priority, string? RelatedEntity);

    [HttpPost("/complaints")]
    public async Task<IActionResult> CreateComplaint([FromBody] CreateComplaintRequest req, CancellationToken ct)
    {
        var id = $"X-{_complaints.Count + 1:D3}";
        var c  = new EjarSeed.ComplaintSeed(
            id, req.Subject, req.Body, DateTime.UtcNow, "open",
            req.Priority ?? "عادي", req.RelatedEntity ?? "",
            new List<EjarSeed.ComplaintReplySeed> {
                new("R1", "user", req.Body, DateTime.UtcNow)
            });

        var op = Entry.Create("complaint.file")
            .Describe($"User files complaint: {req.Subject}")
            .From($"User:{CurrentUserId}", 1, ("role", "complainant"))
            .To($"Complaint:{id}", 1, ("role", "filed"))
            .Tag("complaint_id", id)
            .Analyze(new RequiredFieldAnalyzer("subject", () => req.Subject))
            .Analyze(new RequiredFieldAnalyzer("body",    () => req.Body))
            .Analyze(new MaxLengthAnalyzer("subject",     () => req.Subject, 200))
            .Analyze(new MaxLengthAnalyzer("body",        () => req.Body, 2000))
            .Execute(ctx => { _complaints.Insert(0, c); return Task.CompletedTask; })
            .Build();

        var complaintData = new { id = c.Id, subject = c.Subject, status = c.Status };
        var env = await _engine.ExecuteEnvelopeAsync(op, complaintData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "complaint_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("complaint.file", complaintData);
    }

    public sealed record ReplyRequest(string Message);

    [HttpPost("/complaints/{id}/replies")]
    public async Task<IActionResult> AddReply(string id, [FromBody] ReplyRequest req, CancellationToken ct)
    {
        var ix = _complaints.FindIndex(x => x.Id == id);
        if (ix < 0) return this.NotFoundEnvelope("complaint_not_found");

        var op = Entry.Create("complaint.reply")
            .Describe($"User replies on complaint {id}")
            .From($"User:{CurrentUserId}", 1, ("role", "replier"))
            .To($"Complaint:{id}", 1, ("role", "replied"))
            .Tag("complaint_id", id)
            .Analyze(new RequiredFieldAnalyzer("message", () => req.Message))
            .Analyze(new MaxLengthAnalyzer("message",     () => req.Message, 2000))
            .Execute(ctx =>
            {
                var c = _complaints[ix];
                var replies = c.Replies.Append(new EjarSeed.ComplaintReplySeed(
                    $"R{c.Replies.Count + 1}", "user", req.Message, DateTime.UtcNow)).ToList();
                _complaints[ix] = c with { Replies = replies };
                return Task.CompletedTask;
            })
            .Build();

        var replyData = new { id, repliesCount = _complaints[ix].Replies.Count + 1 };
        var env = await _engine.ExecuteEnvelopeAsync(op, replyData, ct);
        if (env.Operation.Status != "Success")
            return this.BadRequestEnvelope(env.Operation.FailedAnalyzer ?? "reply_failed",
                                           env.Operation.ErrorMessage);
        return this.OkEnvelope("complaint.reply", replyData);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Plans + Subscription + Invoices
    // ═══════════════════════════════════════════════════════════════════════

    [HttpGet("/plans")]
    public IActionResult Plans() =>
        this.OkEnvelope("plans.list",
            EjarSeed.Plans.Select(p => new {
                id = p.Id, name = p.Name, description = p.Description,
                price = p.Price, unit = p.Unit,
                listingQuota = p.ListingQuota, featuredQuota = p.FeaturedQuota,
                imagesPerListing = p.ImagesPerListing,
                popular = p.Popular, features = p.Features
            }));

    [HttpGet("/me/subscription")]
    public IActionResult GetSubscription()
    {
        var s    = EjarSeed.ActiveSubscription;
        var mine = EjarSeed.Listings.Where(l => l.OwnerId == CurrentUserId).ToList();
        return this.OkEnvelope("subscription.get", new {
            id = s.Id, planId = s.PlanId, planName = s.PlanName, status = s.Status,
            startDate = s.StartDate, endDate = s.EndDate,
            daysRemaining = (int)Math.Max(0, (s.EndDate - DateTime.UtcNow).TotalDays),
            listingsUsed = mine.Count(l => l.Status == 1),
            listingsLimit = s.ListingsLimit,
            featuredUsed  = 0, featuredLimit = s.FeaturedLimit,
            imagesPerListing = s.ImagesPerListing
        });
    }

    [HttpGet("/me/invoices")]
    public IActionResult Invoices() =>
        this.OkEnvelope("invoice.list",
            EjarSeed.Invoices.Select(i => new {
                id = i.Id, planId = i.PlanId, amount = i.Amount,
                date = i.Date, status = i.Status
            }));
}
