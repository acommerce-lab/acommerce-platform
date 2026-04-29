using ACommerce.Favorites.Operations.Entities;
using ACommerce.Kits.Support.Domain;
using ACommerce.OperationEngine.Wire.Http;
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
    public CatalogController(EjarDbContext db) => _db = db;

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

    public sealed record UpdateProfileBody(string? FullName, string? Phone, string? Email, string? City);

    [HttpPut("/me/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileBody body, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } id) return this.UnauthorizedEnvelope();
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return this.UnauthorizedEnvelope("user_not_found");

        if (!string.IsNullOrWhiteSpace(body.FullName)) u.FullName = body.FullName!;
        if (body.Phone is not null) u.Phone = body.Phone;
        if (body.Email is not null) u.Email = body.Email;
        if (body.City  is not null) u.City  = body.City;
        u.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return this.OkEnvelope("profile.update", new { id = u.Id, fullName = u.FullName });
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

    // ═══ My Listings ═════════════════════════════════════════════════════
    [HttpGet("/my-listings")]
    public async Task<IActionResult> MyListings(CancellationToken ct)
    {
        if (CurrentUserGuid is not { } id) return this.UnauthorizedEnvelope();
        var rows = await _db.Listings.AsNoTracking()
            .Where(l => l.OwnerId == id).OrderByDescending(l => l.CreatedAt)
            .Select(l => new {
                id = l.Id, title = l.Title, price = l.Price, timeUnit = l.TimeUnit,
                propertyType = l.PropertyType, city = l.City, district = l.District,
                status = l.Status, viewsCount = l.ViewsCount, isVerified = l.IsVerified,
                firstImage = l.ImagesCsv == null ? null : l.ImagesCsv.Substring(0, Math.Max(0, l.ImagesCsv.IndexOf(',') < 0 ? l.ImagesCsv.Length : l.ImagesCsv.IndexOf(',')))
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
        IReadOnlyList<string>? Images);

    [HttpPost("/my-listings")]
    public async Task<IActionResult> CreateListing([FromBody] CreateListingBody body, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } id) return this.UnauthorizedEnvelope();
        if (string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.City))
            return this.BadRequestEnvelope("missing_fields", "title و city مطلوبان");

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
            ImagesCsv = body.Images is null ? "" : string.Join(",", body.Images),
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
                firstImage = l.ImagesCsv?.Split(',').FirstOrDefault()
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
    public sealed record StartConversationBody(string? ListingId, string? PartnerId);

    [HttpPost("/conversations/start")]
    public async Task<IActionResult> StartConversation([FromBody] StartConversationBody body, CancellationToken ct)
    {
        if (CurrentUserGuid is not { } uid) return this.UnauthorizedEnvelope();
        if (string.IsNullOrWhiteSpace(body.ListingId) || string.IsNullOrWhiteSpace(body.PartnerId))
            return this.BadRequestEnvelope("missing_fields");
        if (!Guid.TryParse(body.ListingId, out var listingId) || !Guid.TryParse(body.PartnerId, out var partnerId))
            return this.BadRequestEnvelope("invalid_ids");

        var existing = await _db.Conversations.FirstOrDefaultAsync(
            c => c.ListingId == listingId && c.PartnerId == partnerId, ct);
        if (existing is not null)
            return this.OkEnvelope("conversation.start", new { id = existing.Id });

        var partner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == partnerId, ct);
        var conv = new ConversationEntity {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            PartnerId = partnerId, ListingId = listingId,
            PartnerName = partner?.FullName ?? "—",
            Subject = "محادثة جديدة",
            LastAt = DateTime.UtcNow,
            UnreadCount = 0
        };
        _db.Conversations.Add(conv);
        await _db.SaveChangesAsync(ct);
        return this.OkEnvelope("conversation.start", new { id = conv.Id });
    }
}
