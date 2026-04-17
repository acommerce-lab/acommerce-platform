using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace Order.Web.Operations;

/// <summary>
/// كل عمليات العميل. كل واحدة = Entry.Create + Tag("client_dispatch","true")
/// ليلتقطها HttpDispatchInterceptor ويرسلها للخادم.
/// العمليات المحلية فقط (سلة، تفضيلات UI) لا تحمل client_dispatch.
/// </summary>
public static class ClientOps
{
    // ── Auth ──────────────────────────────────────────────────────────
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

    // ── Auth (sign out — local only, clears store) ──────────────────
    public static Operation SignOut() =>
        Entry.Create("auth.sign_out")
            .Describe("User signs out")
            .From("User:self", 1, ("role", "user"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Build();

    // ── Orders ────────────────────────────────────────────────────────
    public static Operation CreateOrder(Guid? customerId) =>
        Entry.Create("order.create")
            .Describe($"User:{customerId} places order")
            .From($"User:{customerId}", 1, ("role", "customer"))
            .To("System:orders", 1, ("role", "order_service"))
            .Tag("client_dispatch", "true")
            .Build();

    public static Operation CancelOrder(Guid orderId) =>
        Entry.Create("order.cancel")
            .Describe($"Cancel order {orderId}")
            .From($"Order:{orderId}", 1, ("role", "order"))
            .To("System:orders", 1, ("role", "order_service"))
            .Tag("client_dispatch", "true")
            .Tag("order_id", orderId.ToString())
            .Build();

    // ── Favorites ─────────────────────────────────────────────────────
    public static Operation ToggleFavorite(Guid? userId, Guid offerId) =>
        Entry.Create("favorite.toggle")
            .Describe($"User:{userId} toggles favorite Offer:{offerId}")
            .From($"User:{userId}", 1, ("role", "customer"))
            .To($"Offer:{offerId}", 1, ("role", "offer"))
            .Tag("client_dispatch", "true")
            .Build();

    // ── Messages ──────────────────────────────────────────────────────
    public static Operation StartConversation(Guid? customerId, Guid vendorId) =>
        Entry.Create("conversation.start")
            .Describe($"User:{customerId} starts chat with Vendor:{vendorId}")
            .From($"User:{customerId}", 1, ("role", "customer"))
            .To($"Vendor:{vendorId}", 1, ("role", "vendor"))
            .Tag("client_dispatch", "true")
            .Build();

    public static Operation SendMessage(Guid conversationId, Guid? senderId, string? content) =>
        Entry.Create("message.send")
            .Describe($"User:{senderId} sends message in {conversationId}")
            .From($"User:{senderId}", 1, ("role", "sender"))
            .To($"Conversation:{conversationId}", 1, ("role", "conversation"))
            .Tag("client_dispatch", "true")
            .Tag("conversation_id", conversationId.ToString())
            .Analyze(new RequiredFieldAnalyzer("content", () => content))
            .Build();

    public static Operation MarkConversationRead(Guid conversationId, Guid readerId) =>
        Entry.Create("conversation.mark_read")
            .From($"User:{readerId}", 1, ("role", "reader"))
            .To($"Conversation:{conversationId}", 1, ("role", "conversation"))
            .Tag("client_dispatch", "true")
            .Build();

    // ── Notifications ─────────────────────────────────────────────────
    public static Operation MarkNotificationRead(Guid notificationId) =>
        Entry.Create("notification.read")
            .From("User:self", 1, ("role", "reader"))
            .To($"Notification:{notificationId}", 1, ("role", "notification"))
            .Tag("client_dispatch", "true")
            .Build();

    public static Operation MarkAllNotificationsRead(Guid? userId) =>
        Entry.Create("notification.mark_all_read")
            .From($"User:{userId}", 1, ("role", "reader"))
            .To("System:notifications", 1, ("role", "notification_batch"))
            .Tag("client_dispatch", "true")
            .Build();

    // ── Profile ────────────────────────────────────────────────────────
    public static Operation UpdateProfile(Guid userId) =>
        Entry.Create("profile.update")
            .Describe($"User:{userId} updates profile")
            .From($"User:{userId}", 1, ("role", "user"))
            .To($"Profile:{userId}", 1, ("role", "profile"))
            .Tag("client_dispatch", "true")
            .Tag("user_id", userId.ToString())
            .Build();

    // ── Cart (محلّية — لا ترسل HTTP) ──────────────────────────────────
    public static Operation CartAdd(Guid offerId, string title, string emoji, decimal price,
        Guid vendorId, string vendorName, string vendorEmoji) =>
        Entry.Create("cart.add")
            .Describe($"Add {title} to cart")
            .From($"Offer:{offerId}", price, ("role", "offer"))
            .To("Cart:local", price, ("role", "cart"))
            .Tag("offer_id", offerId.ToString())
            .Tag("vendor_id", vendorId.ToString())
            .Tag("vendor_name", vendorName)
            .Tag("vendor_emoji", vendorEmoji)
            .Tag("title", title)
            .Tag("emoji", emoji)
            .Tag("price", price.ToString("0.##"))
            .Build();

    public static Operation CartSetQuantity(Guid offerId, int qty) =>
        Entry.Create("cart.set_quantity")
            .From($"Offer:{offerId}", qty, ("role", "offer"))
            .To("Cart:local", qty, ("role", "cart"))
            .Tag("offer_id", offerId.ToString())
            .Tag("quantity", qty.ToString())
            .Build();

    public static Operation CartClear() =>
        Entry.Create("cart.clear")
            .From("Cart:local", 1, ("role", "cart"))
            .To("System:void", 1, ("role", "void"))
            .Build();

    // ── UI (محلّية — لا ترسل HTTP) ────────────────────────────────────
    public static Operation SetTheme(string theme) =>
        Entry.Create("ui.set_theme")
            .From("User:self", 1, ("role", "user"))
            .To("UI:preferences", 1, ("role", "preferences"))
            .Tag("theme", theme)
            .Build();

    public static Operation SetLanguage(string lang) =>
        Entry.Create("ui.set_language")
            .From("User:self", 1, ("role", "user"))
            .To("UI:preferences", 1, ("role", "preferences"))
            .Tag("language", lang)
            .Build();
}
