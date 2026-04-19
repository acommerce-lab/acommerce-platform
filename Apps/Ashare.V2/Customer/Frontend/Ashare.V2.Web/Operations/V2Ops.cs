using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace Ashare.V2.Web.Operations;

/// <summary>
/// كلّ عمليّات Ashare.V2. كلّ عمليّة = <c>Entry.Create</c> + <c>Tag("client_dispatch","true")</c>
/// ليلتقطها <c>HttpDispatcher</c> ويحوّلها إلى طلب HTTP حسب <see cref="V2Routes"/>.
/// العمليّات المحلّية فقط (UI prefs، سلة recent searches) لا تحمل client_dispatch
/// وتُفسَّر محلّياً عبر <see cref="OperationInterpreterRegistry"/>.
///
/// الفلسفة: الصفحات لا تلمس HttpClient. فقط:
///   <code>await Engine.DispatchAsync&lt;T&gt;(V2Ops.ToggleFavorite(userId, listingId));</code>
/// </summary>
public static class V2Ops
{
    // ═══════════════════════════════════════════════════════════════════
    // Auth
    // ═══════════════════════════════════════════════════════════════════
    public static Operation NafathStart(string nationalId) =>
        Entry.Create("auth.nafath.start")
            .Describe($"Start Nafath for {nationalId}")
            .From("User:anonymous", 1, ("role", "requester"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Tag("client_dispatch", "true")
            .Tag("national_id", nationalId)
            .Analyze(new RequiredFieldAnalyzer("nationalId", () => nationalId))
            .Analyze(new MaxLengthAnalyzer("nationalId", () => nationalId, 10))
            .Build();

    public static Operation SignOut() =>
        Entry.Create("auth.sign_out")
            .Describe("User signs out")
            .From("User:self", 1, ("role", "user"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Build();  // محلّية

    // ═══════════════════════════════════════════════════════════════════
    // Listings (my-listings toggle is must_own — interceptor enforced)
    // ═══════════════════════════════════════════════════════════════════
    public static Operation ToggleListing(string listingId) =>
        Entry.Create("listing.toggle")
            .Describe($"Toggle listing {listingId}")
            .From("User:self", 1, ("role", "owner"))
            .To($"Listing:{listingId}", 1, ("role", "target"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId)
            .Build();

    // ═══════════════════════════════════════════════════════════════════
    // Bookings
    // ═══════════════════════════════════════════════════════════════════
    public static Operation CreateBooking(string listingId, DateTime startDate, int nights, int guests) =>
        Entry.Create("booking.create")
            .Describe($"Book listing {listingId} × {nights} nights")
            .From("User:self", 1, ("role", "booker"))
            .To($"Listing:{listingId}", 1, ("role", "booked"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId)
            .Tag("nights", nights.ToString())
            .Tag("guests", guests.ToString())
            .Tag("start_date", startDate.ToString("yyyy-MM-dd"))
            .Analyze(new RangeAnalyzer("nights", () => nights, 1, 365))
            .Analyze(new RangeAnalyzer("guests", () => guests, 1, 20))
            .Build();

    // ═══════════════════════════════════════════════════════════════════
    // Conversations + Messages
    // ═══════════════════════════════════════════════════════════════════
    public static Operation StartConversation(string listingId, string text) =>
        Entry.Create("conversation.start")
            .Describe($"Open chat on listing {listingId}")
            .From("User:self", 1, ("role", "initiator"))
            .To($"Listing:{listingId}", 1, ("role", "subject"))
            .Tag("client_dispatch", "true")
            .Tag("listing_id", listingId)
            .Analyze(new RequiredFieldAnalyzer("text", () => text))
            .Build();

    public static Operation SendMessage(string conversationId, string text, string? attachment) =>
        Entry.Create("message.send")
            .Describe($"Send message to {conversationId}")
            .From("User:self", 1, ("role", "sender"))
            .To($"Conversation:{conversationId}", 1, ("role", "appended"))
            .Tag("client_dispatch", "true")
            .Tag("conversation_id", conversationId)
            .Build();

    // ═══════════════════════════════════════════════════════════════════
    // Notifications
    // ═══════════════════════════════════════════════════════════════════
    public static Operation ReadNotification(string notificationId) =>
        Entry.Create("notification.read")
            .Describe($"Mark notification {notificationId} read")
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

    // ═══════════════════════════════════════════════════════════════════
    // Complaints
    // ═══════════════════════════════════════════════════════════════════
    public static Operation FileComplaint(string subject, string body, string? priority, string? relatedEntity) =>
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
            .Describe($"Reply on complaint {complaintId}")
            .From("User:self", 1, ("role", "replier"))
            .To($"Complaint:{complaintId}", 1, ("role", "replied"))
            .Tag("client_dispatch", "true")
            .Tag("complaint_id", complaintId)
            .Analyze(new RequiredFieldAnalyzer("message", () => message))
            .Build();

    // ═══════════════════════════════════════════════════════════════════
    // Profile
    // ═══════════════════════════════════════════════════════════════════
    public static Operation UpdateProfile(string fullName, string email, string phone, string city) =>
        Entry.Create("profile.update")
            .Describe("User updates profile")
            .From("User:self", 1, ("role", "user"))
            .To("Profile:self", 1, ("role", "updated"))
            .Tag("client_dispatch", "true")
            .Analyze(new RequiredFieldAnalyzer("fullName", () => fullName))
            .Analyze(new RequiredFieldAnalyzer("phone", () => phone))
            .Build();

    // ═══════════════════════════════════════════════════════════════════
    // UI prefs (محلّية — لا تُرسل HTTP)
    // ═══════════════════════════════════════════════════════════════════
    public static Operation SetTheme(string theme) =>
        Entry.Create("ui.set_theme")
            .From("User:self", 1, ("role", "user"))
            .To("UI:preferences", 1, ("role", "preferences"))
            .Tag("theme", theme)
            .Build();

    // ── Culture: language + timezone + currency — وحدة تتغيّر معاً.
    //    المفسّر يدمج الوسوم الموجودة مع الثقافة الحاليّة فلا حاجة لإعادة إرسال الثلاثة.
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
    public static Operation SetTimeZone(string tz)   => SetCulture(timezone: tz);
    public static Operation SetCurrency(string cur)  => SetCulture(currency: cur);

    public static Operation SetCity(string city) =>
        Entry.Create("ui.set_city")
            .From("User:self", 1, ("role", "user"))
            .To("UI:preferences", 1, ("role", "preferences"))
            .Tag("city", city)
            .Build();

    public static Operation ToggleFavorite(string listingId) =>
        Entry.Create("favorite.toggle")
            .From("User:self", 1, ("role", "user"))
            .To($"Listing:{listingId}", 1, ("role", "target"))
            .Tag("listing_id", listingId)
            .Build();

    public static Operation AddRecentSearch(string query) =>
        Entry.Create("ui.recent_search.add")
            .From("User:self", 1, ("role", "user"))
            .To("UI:searches", 1, ("role", "history"))
            .Tag("query", query)
            .Build();
}
