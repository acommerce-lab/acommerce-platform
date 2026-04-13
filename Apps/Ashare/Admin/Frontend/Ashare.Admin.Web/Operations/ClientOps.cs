using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace Ashare.Admin.Web.Operations;

public static class ClientOps
{
    // ── Auth ──────────────────────────────────────────────────────────────
    public static Operation RequestOtp(string phone) =>
        Entry.Create("auth.sms.request")
            .Describe($"Admin requests OTP for {phone}")
            .From("User:anonymous", 1, ("role", "requester"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Tag("client_dispatch", "true")
            .Tag("phone_number", phone)
            .Analyze(new RequiredFieldAnalyzer("phone", () => phone))
            .Build();

    public static Operation VerifyOtp(Guid userId, string challengeId, string code) =>
        Entry.Create("auth.sms.verify")
            .Describe($"Admin verifies OTP for User:{userId}")
            .From($"User:{userId}", 1, ("role", "admin"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Tag("client_dispatch", "true")
            .Tag("user_id", userId.ToString())
            .Tag("challenge_id", challengeId)
            .Analyze(new RequiredFieldAnalyzer("code", () => code))
            .Build();

    public static Operation SignOut() =>
        Entry.Create("auth.sign_out")
            .Describe("Admin signs out")
            .From("User:self", 1, ("role", "admin"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Build();

    // ── UI ─────────────────────────────────────────────────────────────────
    public static Operation SetTheme(string theme) =>
        Entry.Create("ui.set_theme")
            .Describe($"Set theme to {theme}")
            .From("User:self", 1, ("role", "admin"))
            .To("System:ui", 1, ("role", "ui_service"))
            .Tag("theme", theme)
            .Build();

    public static Operation SetLanguage(string language) =>
        Entry.Create("ui.set_language")
            .Describe($"Set language to {language}")
            .From("User:self", 1, ("role", "admin"))
            .To("System:ui", 1, ("role", "ui_service"))
            .Tag("language", language)
            .Build();

    // ── Admin User Actions ────────────────────────────────────────────────
    public static Operation SuspendUser(Guid userId) =>
        Entry.Create("admin.user.suspend")
            .Describe($"Admin suspends User:{userId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"User:{userId}", 1, ("role", "target_user"))
            .Tag("client_dispatch", "true")
            .Tag("user_id", userId.ToString())
            .Build();

    public static Operation ActivateUser(Guid userId) =>
        Entry.Create("admin.user.activate")
            .Describe($"Admin activates User:{userId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"User:{userId}", 1, ("role", "target_user"))
            .Tag("client_dispatch", "true")
            .Tag("user_id", userId.ToString())
            .Build();

    // ── Admin Listing Actions ─────────────────────────────────────────────
    public static Operation ApproveListing(Guid listingId) =>
        Entry.Create("admin.listing.approve")
            .Describe($"Admin approves Listing:{listingId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId.ToString())
            .Build();

    public static Operation RejectListing(Guid listingId) =>
        Entry.Create("admin.listing.reject")
            .Describe($"Admin rejects Listing:{listingId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId.ToString())
            .Build();

    public static Operation FeatureListing(Guid listingId) =>
        Entry.Create("admin.listing.feature")
            .Describe($"Admin toggles featured on Listing:{listingId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId.ToString())
            .Build();

    public static Operation DeleteListing(Guid listingId) =>
        Entry.Create("admin.listing.delete")
            .Describe($"Admin deletes Listing:{listingId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId.ToString())
            .Build();

    // ── Admin Subscription Actions ────────────────────────────────────────
    public static Operation CancelSubscription(Guid subscriptionId) =>
        Entry.Create("admin.subscription.cancel")
            .Describe($"Admin cancels Subscription:{subscriptionId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Subscription:{subscriptionId}", 1, ("role", "subscription"))
            .Tag("client_dispatch", "true")
            .Tag("subscription_id", subscriptionId.ToString())
            .Build();

    // ── Admin Notification Broadcast ──────────────────────────────────────
    public static Operation BroadcastNotification(string title, string body) =>
        Entry.Create("admin.notification.broadcast")
            .Describe("Admin broadcasts notification")
            .From("User:self", 1, ("role", "admin"))
            .To("System:notifications", 1, ("role", "notification_service"))
            .Tag("client_dispatch", "true")
            .Tag("title", title)
            .Tag("body", body)
            .Build();
}
