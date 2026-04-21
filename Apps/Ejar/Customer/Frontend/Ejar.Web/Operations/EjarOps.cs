using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace Ejar.Web.Operations;

public static class EjarOps
{
    // ── Auth ──────────────────────────────────────────────────────────
    public static Operation RequestOtp(string phone) =>
        Entry.Create("auth.otp.request")
            .Describe($"Request OTP for {phone}")
            .From("User:anonymous", 1, ("role", "requester"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Tag("client_dispatch", "true")
            .Tag("phone", phone)
            .Analyze(new RequiredFieldAnalyzer("phone", () => phone))
            .Build();

    public static Operation VerifyOtp(string phone, string code) =>
        Entry.Create("auth.otp.verify")
            .Describe($"Verify OTP for {phone}")
            .From("User:anonymous", 1, ("role", "verifier"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Tag("client_dispatch", "true")
            .Tag("phone", phone)
            .Tag("code", code)
            .Analyze(new RequiredFieldAnalyzer("phone", () => phone))
            .Analyze(new RequiredFieldAnalyzer("code", () => code))
            .Build();

    public static Operation SignOut() =>
        Entry.Create("auth.sign_out")
            .Describe("User signs out")
            .From("User:self", 1, ("role", "user"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Build();

    // ── My Listings ────────────────────────────────────────────────────
    public static Operation ToggleListing(string listingId) =>
        Entry.Create("listing.toggle")
            .From("User:self", 1, ("role", "owner"))
            .To($"Listing:{listingId}", 1, ("role", "target"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId)
            .Build();

    public static Operation CreateListing(string title, string categoryId, string city, decimal price, string timeUnit) =>
        Entry.Create("listing.create")
            .Describe($"Create listing: {title}")
            .From("User:self", 1, ("role", "owner"))
            .To("System:listings", 1, ("role", "created"))
            .Tag("client_dispatch", "true")
            .Analyze(new RequiredFieldAnalyzer("title", () => title))
            .Analyze(new RequiredFieldAnalyzer("city", () => city))
            .Analyze(new MaxLengthAnalyzer("title", () => title, 200))
            .Build();

    public static Operation DeleteListing(string listingId) =>
        Entry.Create("listing.delete")
            .From("User:self", 1, ("role", "owner"))
            .To($"Listing:{listingId}", -1, ("role", "deleted"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId)
            .Build();

    // ── Favorites ─────────────────────────────────────────────────────
    public static Operation ToggleFavorite(string listingId) =>
        Entry.Create("favorite.toggle")
            .From("User:self", 1, ("role", "user"))
            .To($"Listing:{listingId}", 1, ("role", "target"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId)
            .Build();

    // ── Conversations + Messages ───────────────────────────────────────
    public static Operation StartConversation(string listingId, string text) =>
        Entry.Create("conversation.start")
            .Describe($"Open chat on listing {listingId}")
            .From("User:self", 1, ("role", "initiator"))
            .To($"Listing:{listingId}", 1, ("role", "subject"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId)
            .Analyze(new RequiredFieldAnalyzer("text", () => text))
            .Build();

    public static Operation SendMessage(string conversationId, string text) =>
        Entry.Create("message.send")
            .From("User:self", 1, ("role", "sender"))
            .To($"Conversation:{conversationId}", 1, ("role", "appended"))
            .Tag("client_dispatch", "true")
            .Tag("conversation_id", conversationId)
            .Analyze(new RequiredFieldAnalyzer("text", () => text))
            .Build();

    // ── Notifications ─────────────────────────────────────────────────
    public static Operation ReadNotification(string notificationId) =>
        Entry.Create("notification.read")
            .From("User:self", 1, ("role", "reader"))
            .To($"Notification:{notificationId}", 1, ("role", "read"))
            .Tag("client_dispatch", "true")
            .Tag("notification_id", notificationId)
            .Build();

    public static Operation ReadAllNotifications() =>
        Entry.Create("notification.read.all")
            .From("User:self", 1, ("role", "reader"))
            .To("System:notifications", 1, ("role", "batch"))
            .Tag("client_dispatch", "true")
            .Build();

    // ── Complaints ────────────────────────────────────────────────────
    public static Operation FileComplaint(string subject, string body) =>
        Entry.Create("complaint.file")
            .Describe($"File complaint: {subject}")
            .From("User:self", 1, ("role", "complainant"))
            .To("System:complaints", 1, ("role", "tracker"))
            .Tag("client_dispatch", "true")
            .Analyze(new RequiredFieldAnalyzer("subject", () => subject))
            .Analyze(new RequiredFieldAnalyzer("body", () => body))
            .Analyze(new MaxLengthAnalyzer("subject", () => subject, 200))
            .Analyze(new MaxLengthAnalyzer("body", () => body, 2000))
            .Build();

    public static Operation ReplyComplaint(string complaintId, string message) =>
        Entry.Create("complaint.reply")
            .From("User:self", 1, ("role", "replier"))
            .To($"Complaint:{complaintId}", 1, ("role", "replied"))
            .Tag("client_dispatch", "true")
            .Tag("complaint_id", complaintId)
            .Analyze(new RequiredFieldAnalyzer("message", () => message))
            .Build();

    // ── Profile ───────────────────────────────────────────────────────
    public static Operation UpdateProfile(string fullName, string email, string phone, string city) =>
        Entry.Create("profile.update")
            .Describe("User updates profile")
            .From("User:self", 1, ("role", "user"))
            .To("Profile:self", 1, ("role", "updated"))
            .Tag("client_dispatch", "true")
            .Analyze(new RequiredFieldAnalyzer("fullName", () => fullName))
            .Analyze(new RequiredFieldAnalyzer("phone", () => phone))
            .Build();

    // ── UI prefs (local — no HTTP) ─────────────────────────────────────
    public static Operation SetTheme(string theme) =>
        Entry.Create("ui.set_theme")
            .From("User:self", 1, ("role", "user"))
            .To("UI:preferences", 1, ("role", "preferences"))
            .Tag("theme", theme)
            .Build();

    public static Operation SetCulture(string? language = null, string? timezone = null, string? currency = null)
    {
        var e = Entry.Create("ui.set_culture")
            .From("User:self", 1, ("role", "user"))
            .To("UI:culture", 1, ("role", "culture"));
        if (!string.IsNullOrEmpty(language)) e = e.Tag("language", language);
        if (!string.IsNullOrEmpty(timezone)) e = e.Tag("timezone", timezone);
        if (!string.IsNullOrEmpty(currency)) e = e.Tag("currency", currency);
        return e.Build();
    }

    public static Operation SetLanguage(string lang) => SetCulture(language: lang);

    public static Operation SetCity(string city) =>
        Entry.Create("ui.set_city")
            .From("User:self", 1, ("role", "user"))
            .To("UI:preferences", 1, ("role", "preferences"))
            .Tag("city", city)
            .Build();

    public static Operation AddRecentSearch(string query) =>
        Entry.Create("ui.recent_search.add")
            .From("User:self", 1, ("role", "user"))
            .To("UI:searches", 1, ("role", "history"))
            .Tag("query", query)
            .Build();
}
