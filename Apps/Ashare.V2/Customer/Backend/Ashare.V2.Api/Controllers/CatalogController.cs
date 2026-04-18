using ACommerce.OperationEngine.Wire.Http;
using Ashare.V2.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.V2.Api.Controllers;

/// <summary>
/// Endpoints إضافيّة لـ Ashare.V2: cities, plans, legal, version.
/// </summary>
[ApiController]
public class CatalogController : ControllerBase
{
    [HttpGet("/cities")]
    public IActionResult Cities() =>
        this.OkEnvelope("cities.list", AshareV2Seed.Cities);

    [HttpGet("/plans")]
    public IActionResult Plans() =>
        this.OkEnvelope("plans.list",
            AshareV2Seed.Plans.Select(p => new {
                id = p.Id, name = p.Name, description = p.Description,
                price = p.Price, unit = p.Unit,
                listingQuota = p.ListingQuota, featuredQuota = p.FeaturedQuota,
                popular = p.Popular
            }));

    [HttpGet("/legal/{key}")]
    public IActionResult Legal(string key)
    {
        var doc = AshareV2Seed.Legal.FirstOrDefault(l => l.Key == key);
        if (doc is null) return this.NotFoundEnvelope("legal_not_found", $"Key '{key}' not found");
        return this.OkEnvelope("legal.fetch", new { key = doc.Key, title = doc.Title, body = doc.Body });
    }

    [HttpGet("/version/check")]
    public IActionResult VersionCheck() =>
        this.OkEnvelope("app.version.check", new {
            current  = AshareV2Seed.Version.Current,
            latest   = AshareV2Seed.Version.Latest,
            isBlocked= AshareV2Seed.Version.IsBlocked,
            storeUrl = AshareV2Seed.Version.StoreUrl,
            supportEmail = AshareV2Seed.Version.SupportEmail
        });

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

    [HttpGet("/conversations")]
    public IActionResult Conversations() =>
        this.OkEnvelope("conversation.list",
            AshareV2Seed.Conversations.Select(c => new {
                id = c.Id, partnerName = c.PartnerName, subject = c.Subject,
                lastAt = c.LastAt, lastMessage = c.Messages.LastOrDefault()?.Text ?? ""
            }));

    [HttpGet("/conversations/{id}")]
    public IActionResult Conversation(string id)
    {
        var c = AshareV2Seed.Conversations.FirstOrDefault(x => x.Id == id);
        if (c is null) return this.NotFoundEnvelope("conversation_not_found");
        return this.OkEnvelope("conversation.details", new {
            id = c.Id, partnerName = c.PartnerName, subject = c.Subject,
            messages = c.Messages.Select(m => new {
                id = m.Id, from = m.From, text = m.Text, sentAt = m.SentAt
            })
        });
    }

    [HttpGet("/complaints")]
    public IActionResult Complaints() =>
        this.OkEnvelope("complaint.list",
            AshareV2Seed.Complaints.Select(c => new {
                id = c.Id, subject = c.Subject, body = c.Body,
                createdAt = c.CreatedAt, status = c.Status
            }));

    [HttpGet("/my-listings")]
    public IActionResult MyListings() =>
        this.OkEnvelope("listing.my",
            AshareV2Seed.Listings.Take(3).Select(l => new {
                id = l.Id, title = l.Title, price = l.Price, currency = "SAR",
                timeUnit = l.TimeUnit, city = l.City, district = l.District,
                isFeatured = l.IsFeatured, status = 1
            }));
}
