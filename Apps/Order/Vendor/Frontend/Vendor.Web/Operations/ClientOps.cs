using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace Vendor.Web.Operations;

/// <summary>
/// كل عمليات العميل. كل واحدة = Entry.Create + Tag("client_dispatch","true")
/// ليلتقطها HttpDispatchInterceptor ويرسلها للخادم.
/// العمليات المحلية فقط (تفضيلات UI) لا تحمل client_dispatch.
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

    // ── Vendor Orders ────────────────────────────────────────────────
    public static Operation AcceptOrder(Guid orderId) =>
        Entry.Create("vendor-order.accept")
            .Describe($"Vendor accepts order {orderId}")
            .From($"Order:{orderId}", 1, ("role", "order"))
            .To("System:vendor_orders", 1, ("role", "vendor_order_service"))
            .Tag("client_dispatch", "true")
            .Tag("order_id", orderId.ToString())
            .Build();

    public static Operation RejectOrder(Guid orderId) =>
        Entry.Create("vendor-order.reject")
            .Describe($"Vendor rejects order {orderId}")
            .From($"Order:{orderId}", 1, ("role", "order"))
            .To("System:vendor_orders", 1, ("role", "vendor_order_service"))
            .Tag("client_dispatch", "true")
            .Tag("order_id", orderId.ToString())
            .Build();

    public static Operation ReadyOrder(Guid orderId) =>
        Entry.Create("vendor-order.ready")
            .Describe($"Vendor marks order {orderId} ready")
            .From($"Order:{orderId}", 1, ("role", "order"))
            .To("System:vendor_orders", 1, ("role", "vendor_order_service"))
            .Tag("client_dispatch", "true")
            .Tag("order_id", orderId.ToString())
            .Build();

    public static Operation DeliverOrder(Guid orderId) =>
        Entry.Create("vendor-order.deliver")
            .Describe($"Vendor marks order {orderId} delivered")
            .From($"Order:{orderId}", 1, ("role", "order"))
            .To("System:vendor_orders", 1, ("role", "vendor_order_service"))
            .Tag("client_dispatch", "true")
            .Tag("order_id", orderId.ToString())
            .Build();

    // ── Offers ────────────────────────────────────────────────────────
    public static Operation CreateOffer(Guid vendorId) =>
        Entry.Create("offer.create")
            .Describe($"Vendor:{vendorId} creates new offer")
            .From($"Vendor:{vendorId}", 1, ("role", "vendor"))
            .To("System:offers", 1, ("role", "offer_service"))
            .Tag("client_dispatch", "true")
            .Tag("vendor_id", vendorId.ToString())
            .Build();

    public static Operation UpdateOffer(Guid offerId) =>
        Entry.Create("offer.update")
            .Describe($"Update offer {offerId}")
            .From($"Offer:{offerId}", 1, ("role", "offer"))
            .To("System:offers", 1, ("role", "offer_service"))
            .Tag("client_dispatch", "true")
            .Tag("offer_id", offerId.ToString())
            .Build();

    // ── Messages ──────────────────────────────────────────────────────
    public static Operation SendMessage(Guid conversationId, Guid? senderId, string content) =>
        Entry.Create("message.send")
            .Describe($"Vendor:{senderId} sends message in {conversationId}")
            .From($"User:{senderId}", 1, ("role", "sender"))
            .To($"Conversation:{conversationId}", 1, ("role", "conversation"))
            .Tag("client_dispatch", "true")
            .Tag("conversation_id", conversationId.ToString())
            .Analyze(new RequiredFieldAnalyzer("content", () => content))
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
