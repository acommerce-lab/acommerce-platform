using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace Ashare.V2.Provider.Web.Operations;

public static class ProviderV2Ops
{
    // ── Auth (Nafath) ──────────────────────────────────────────────────────
    public static Operation NafathStart(string nationalId) =>
        Entry.Create("auth.nafath.start")
            .Describe($"Start Nafath for NationalId:{nationalId}")
            .From("User:anonymous", 1, ("role", "requester"))
            .To("System:auth", 1, ("role", "nafath"))
            .Tag("client_dispatch", "true")
            .Tag("national_id", nationalId)
            .Analyze(new RequiredFieldAnalyzer("national_id", () => nationalId))
            .Build();

    public static Operation SignOut() =>
        Entry.Create("auth.sign_out")
            .Describe("Provider signs out")
            .From("User:self", 1, ("role", "provider"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Build();

    // ── Listings ──────────────────────────────────────────────────────────
    public static Operation ToggleListing(Guid ownerId, string listingId) =>
        Entry.Create("listing.toggle")
            .Describe($"Owner:{ownerId} toggles Listing:{listingId}")
            .From($"User:{ownerId}", 1, ("role", "owner"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId)
            .Build();

    // ── Bookings ──────────────────────────────────────────────────────────
    public static Operation ConfirmBooking(Guid bookingId) =>
        Entry.Create("booking.confirm")
            .Describe($"Owner confirms Booking:{bookingId}")
            .From($"Booking:{bookingId}", 1, ("role", "booking"))
            .To("System:bookings", 1, ("role", "booking_service"))
            .Tag("client_dispatch", "true")
            .Tag("booking_id", bookingId.ToString())
            .Build();

    public static Operation RejectBooking(Guid bookingId) =>
        Entry.Create("booking.reject")
            .Describe($"Owner rejects Booking:{bookingId}")
            .From($"Booking:{bookingId}", 1, ("role", "booking"))
            .To("System:bookings", 1, ("role", "booking_service"))
            .Tag("client_dispatch", "true")
            .Tag("booking_id", bookingId.ToString())
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
