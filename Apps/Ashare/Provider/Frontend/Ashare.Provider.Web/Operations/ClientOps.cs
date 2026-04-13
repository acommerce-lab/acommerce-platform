using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace Ashare.Provider.Web.Operations;

public static class ClientOps
{
    // ── Auth ──────────────────────────────────────────────────────────────
    public static Operation RequestOtp(string phone) =>
        Entry.Create("auth.sms.request")
            .Describe($"Request OTP for {phone}")
            .From("User:anonymous", 1, ("role", "requester"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Tag("client_dispatch", "true")
            .Tag("phone_number", phone)
            .Analyze(new RequiredFieldAnalyzer("phone", () => phone))
            .Build();

    public static Operation VerifyOtp(Guid userId, string challengeId, string code) =>
        Entry.Create("auth.sms.verify")
            .Describe($"Verify OTP for User:{userId}")
            .From($"User:{userId}", 1, ("role", "user"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Tag("client_dispatch", "true")
            .Tag("user_id", userId.ToString())
            .Tag("challenge_id", challengeId)
            .Analyze(new RequiredFieldAnalyzer("code", () => code))
            .Build();

    public static Operation SignOut() =>
        Entry.Create("auth.sign_out")
            .Describe("Owner signs out")
            .From("User:self", 1, ("role", "user"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Build();

    // ── Listings (owner management) ───────────────────────────────────────
    public static Operation CreateListing(Guid ownerId, string title) =>
        Entry.Create("listing.create")
            .Describe($"Owner:{ownerId} creates listing: {title}")
            .From($"User:{ownerId}", 1, ("role", "owner"))
            .To("System:listings", 1, ("role", "listing_service"))
            .Tag("client_dispatch", "true")
            .Analyze(new RequiredFieldAnalyzer("title", () => title))
            .Build();

    public static Operation UpdateListing(Guid ownerId, Guid listingId) =>
        Entry.Create("listing.update")
            .Describe($"Owner:{ownerId} updates Listing:{listingId}")
            .From($"User:{ownerId}", 1, ("role", "owner"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId.ToString())
            .Build();

    public static Operation DeleteListing(Guid ownerId, Guid listingId) =>
        Entry.Create("listing.delete")
            .Describe($"Owner:{ownerId} deletes Listing:{listingId}")
            .From($"User:{ownerId}", 1, ("role", "owner"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId.ToString())
            .Build();

    public static Operation FeatureListing(Guid ownerId, Guid listingId) =>
        Entry.Create("listing.feature")
            .Describe($"Owner:{ownerId} toggles featured on Listing:{listingId}")
            .From($"User:{ownerId}", 1, ("role", "owner"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId.ToString())
            .Build();

    // ── Bookings (owner responses) ─────────────────────────────────────────
    public static Operation ConfirmBooking(Guid bookingId) =>
        Entry.Create("booking.confirm")
            .Describe($"Owner confirms booking {bookingId}")
            .From($"Booking:{bookingId}", 1, ("role", "booking"))
            .To("System:bookings", 1, ("role", "booking_service"))
            .Tag("client_dispatch", "true")
            .Tag("booking_id", bookingId.ToString())
            .Build();

    public static Operation RejectBooking(Guid bookingId) =>
        Entry.Create("booking.reject")
            .Describe($"Owner rejects booking {bookingId}")
            .From($"Booking:{bookingId}", 1, ("role", "booking"))
            .To("System:bookings", 1, ("role", "booking_service"))
            .Tag("client_dispatch", "true")
            .Tag("booking_id", bookingId.ToString())
            .Build();

    // ── Profile ────────────────────────────────────────────────────────────
    public static Operation UpdateProfile(Guid userId) =>
        Entry.Create("profile.upsert")
            .Describe($"Owner:{userId} updates profile")
            .From($"User:{userId}", 1, ("role", "owner"))
            .To($"Profile:{userId}", 1, ("role", "profile"))
            .Tag("client_dispatch", "true")
            .Tag("user_id", userId.ToString())
            .Build();

    // ── Subscriptions ─────────────────────────────────────────────────────
    public static Operation Subscribe(Guid userId, Guid planId) =>
        Entry.Create("subscription.create")
            .Describe($"Owner:{userId} subscribes to Plan:{planId}")
            .From($"User:{userId}", 1, ("role", "subscriber"))
            .To($"Plan:{planId}", 1, ("role", "plan"))
            .Tag("client_dispatch", "true")
            .Build();

    public static Operation CancelSubscription(Guid userId, Guid subscriptionId) =>
        Entry.Create("subscription.cancel")
            .Describe($"Owner:{userId} cancels Subscription:{subscriptionId}")
            .From($"User:{userId}", 1, ("role", "subscriber"))
            .To($"Subscription:{subscriptionId}", 1, ("role", "subscription"))
            .Tag("client_dispatch", "true")
            .Tag("subscription_id", subscriptionId.ToString())
            .Build();

    // ── UI ─────────────────────────────────────────────────────────────────
    public static Operation SetTheme(string theme) =>
        Entry.Create("ui.set_theme")
            .Describe($"Set theme to {theme}")
            .From("User:self", 1, ("role", "user"))
            .To("System:ui", 1, ("role", "ui_service"))
            .Tag("theme", theme)
            .Build();

    public static Operation SetLanguage(string language) =>
        Entry.Create("ui.set_language")
            .Describe($"Set language to {language}")
            .From("User:self", 1, ("role", "user"))
            .To("System:ui", 1, ("role", "ui_service"))
            .Tag("language", language)
            .Build();
}
