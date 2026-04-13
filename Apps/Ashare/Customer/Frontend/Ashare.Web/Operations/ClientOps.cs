using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace Ashare.Web.Operations;

/// <summary>
/// كل عمليات عميل Ashare.Web.
/// كل mutation = Entry.Create + Tag("client_dispatch","true") ليلتقطه HttpDispatchInterceptor.
/// العمليات المحلية فقط (UI) لا تحمل client_dispatch.
/// </summary>
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
            .Describe("User signs out")
            .From("User:self", 1, ("role", "user"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Build();

    // ── Listings ──────────────────────────────────────────────────────────
    public static Operation CreateListing(Guid ownerId, string title) =>
        Entry.Create("listing.create")
            .Describe($"User:{ownerId} creates listing: {title}")
            .From($"User:{ownerId}", 1, ("role", "owner"))
            .To("System:listings", 1, ("role", "listing_service"))
            .Tag("client_dispatch", "true")
            .Analyze(new RequiredFieldAnalyzer("title", () => title))
            .Build();

    // ── Bookings ──────────────────────────────────────────────────────────
    public static Operation CreateBooking(Guid userId, Guid listingId) =>
        Entry.Create("booking.create")
            .Describe($"User:{userId} books Listing:{listingId}")
            .From($"User:{userId}", 1, ("role", "customer"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true")
            .Build();

    public static Operation CancelBooking(Guid bookingId) =>
        Entry.Create("booking.cancel")
            .Describe($"Cancel booking {bookingId}")
            .From($"Booking:{bookingId}", 1, ("role", "booking"))
            .To("System:bookings", 1, ("role", "booking_service"))
            .Tag("client_dispatch", "true")
            .Tag("booking_id", bookingId.ToString())
            .Build();

    public static Operation PayBooking(Guid bookingId) =>
        Entry.Create("booking.pay")
            .Describe($"Pay for booking {bookingId}")
            .From($"Booking:{bookingId}", 1, ("role", "booking"))
            .To("System:payments", 1, ("role", "payment_service"))
            .Tag("client_dispatch", "true")
            .Tag("booking_id", bookingId.ToString())
            .Build();

    // ── Listings (owner actions) ───────────────────────────────────────────
    public static Operation UpdateListing(Guid ownerId, Guid listingId) =>
        Entry.Create("listing.update")
            .Describe($"User:{ownerId} updates Listing:{listingId}")
            .From($"User:{ownerId}", 1, ("role", "owner"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId.ToString())
            .Build();

    public static Operation DeleteListing(Guid ownerId, Guid listingId) =>
        Entry.Create("listing.delete")
            .Describe($"User:{ownerId} deletes Listing:{listingId}")
            .From($"User:{ownerId}", 1, ("role", "owner"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId.ToString())
            .Build();

    public static Operation FeatureListing(Guid ownerId, Guid listingId) =>
        Entry.Create("listing.feature")
            .Describe($"User:{ownerId} toggles featured on Listing:{listingId}")
            .From($"User:{ownerId}", 1, ("role", "owner"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId.ToString())
            .Build();

    // ── Bookings (owner actions) ───────────────────────────────────────────
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
            .Describe($"User:{userId} updates profile")
            .From($"User:{userId}", 1, ("role", "owner"))
            .To($"Profile:{userId}", 1, ("role", "profile"))
            .Tag("client_dispatch", "true")
            .Tag("user_id", userId.ToString())
            .Build();

    // ── Favorites ──────────────────────────────────────────────────────────
    public static Operation AddFavorite(Guid userId, Guid entityId, string entityType) =>
        Entry.Create("favorite.add")
            .Describe($"User:{userId} favorites {entityType}:{entityId}")
            .From($"User:{userId}", 1, ("role", "user"))
            .To($"{entityType}:{entityId}", 1, ("role", "entity"))
            .Tag("client_dispatch", "true")
            .Tag("entity_type", entityType)
            .Tag("entity_id", entityId.ToString())
            .Build();

    public static Operation RemoveFavorite(Guid userId, Guid entityId, string entityType) =>
        Entry.Create("favorite.remove")
            .Describe($"User:{userId} unfavorites {entityType}:{entityId}")
            .From($"User:{userId}", 1, ("role", "user"))
            .To($"{entityType}:{entityId}", 1, ("role", "entity"))
            .Tag("client_dispatch", "true")
            .Tag("entity_type", entityType)
            .Tag("entity_id", entityId.ToString())
            .Build();

    // ── Media ──────────────────────────────────────────────────────────────
    public static Operation UploadMedia(Guid uploaderId, string directory = "listings") =>
        Entry.Create("media.upload")
            .Describe($"User:{uploaderId} uploads media to {directory}")
            .From($"User:{uploaderId}", 1, ("role", "uploader"))
            .To($"Storage:{directory}", 1, ("role", "storage"))
            .Tag("client_dispatch", "true")
            .Tag("directory", directory)
            .Build();

    // ── Subscriptions ─────────────────────────────────────────────────────
    public static Operation Subscribe(Guid userId, Guid planId) =>
        Entry.Create("subscription.create")
            .Describe($"User:{userId} subscribes to Plan:{planId}")
            .From($"User:{userId}", 1, ("role", "subscriber"))
            .To($"Plan:{planId}", 1, ("role", "plan"))
            .Tag("client_dispatch", "true")
            .Build();

    public static Operation CancelSubscription(Guid userId, Guid subscriptionId) =>
        Entry.Create("subscription.cancel")
            .Describe($"User:{userId} cancels Subscription:{subscriptionId}")
            .From($"User:{userId}", 1, ("role", "subscriber"))
            .To($"Subscription:{subscriptionId}", 1, ("role", "subscription"))
            .Tag("client_dispatch", "true")
            .Tag("subscription_id", subscriptionId.ToString())
            .Build();

    // ── Messages ──────────────────────────────────────────────────────────
    public static Operation SendMessage(Guid conversationId, Guid senderId, string content) =>
        Entry.Create("message.send")
            .Describe($"User:{senderId} sends message in Conversation:{conversationId}")
            .From($"User:{senderId}", 1, ("role", "sender"))
            .To($"Conversation:{conversationId}", 1, ("role", "conversation"))
            .Tag("client_dispatch", "true")
            .Tag("conversation_id", conversationId.ToString())
            .Analyze(new RequiredFieldAnalyzer("content", () => content))
            .Build();

    // ── Notifications ─────────────────────────────────────────────────────
    public static Operation ReadNotification(Guid notificationId) =>
        Entry.Create("notification.read")
            .Describe($"Mark notification {notificationId} as read")
            .From($"Notification:{notificationId}", 1, ("role", "notification"))
            .To("System:notifications", 1, ("role", "notification_service"))
            .Tag("client_dispatch", "true")
            .Tag("notification_id", notificationId.ToString())
            .Build();

    public static Operation MarkAllNotificationsRead(Guid userId) =>
        Entry.Create("notification.mark_all_read")
            .Describe($"Mark all notifications read for User:{userId}")
            .From($"User:{userId}", 1, ("role", "user"))
            .To("System:notifications", 1, ("role", "notification_service"))
            .Tag("client_dispatch", "true")
            .Tag("user_id", userId.ToString())
            .Build();

    public static Operation SendTestNotification(Guid userId, string title, string body) =>
        Entry.Create("notification.send_test")
            .Describe($"Send test notification to User:{userId}")
            .From($"User:{userId}", 1, ("role", "user"))
            .To("System:notifications", 1, ("role", "notification_service"))
            .Tag("client_dispatch", "true")
            .Tag("user_id", userId.ToString())
            .Build();

    // ── Conversations ─────────────────────────────────────────────────────
    public static Operation StartConversation(Guid listingId, Guid? customerId) =>
        Entry.Create("conversation.start")
            .Describe($"Start conversation for Listing:{listingId}")
            .From($"User:{customerId}", 1, ("role", "customer"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId.ToString())
            .Build();

    // ── Payments ──────────────────────────────────────────────────────────
    public static Operation InitiatePayment(Guid bookingId) =>
        Entry.Create("payment.initiate")
            .Describe($"Initiate payment for Booking:{bookingId}")
            .From($"Booking:{bookingId}", 1, ("role", "booking"))
            .To("System:payments", 1, ("role", "payment_service"))
            .Tag("client_dispatch", "true")
            .Tag("booking_id", bookingId.ToString())
            .Build();

    public static Operation SimulatePaymentCallback(Guid paymentId) =>
        Entry.Create("payment.simulate_callback")
            .Describe($"Simulate payment callback for Payment:{paymentId}")
            .From($"Payment:{paymentId}", 1, ("role", "payment"))
            .To("System:payments", 1, ("role", "payment_service"))
            .Tag("client_dispatch", "true")
            .Tag("payment_id", paymentId.ToString())
            .Build();

    // ── UI (local — no HTTP) ──────────────────────────────────────────────
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
