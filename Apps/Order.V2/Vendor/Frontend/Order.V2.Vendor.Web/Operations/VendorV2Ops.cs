using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace Order.V2.Vendor.Web.Operations;

public static class VendorV2Ops
{
    // ── Auth ──────────────────────────────────────────────────────────────
    public static Operation RequestOtp(string phone) =>
        Entry.Create("auth.sms.request")
            .Describe($"Vendor requests OTP for {phone}")
            .From("User:anonymous", 1, ("role", "requester"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Tag("client_dispatch", "true").Tag("phone_number", phone)
            .Analyze(new RequiredFieldAnalyzer("phone", () => phone))
            .Build();

    public static Operation VerifyOtp(Guid userId, string challengeId, string code) =>
        Entry.Create("auth.sms.verify")
            .Describe($"Vendor verifies OTP for User:{userId}")
            .From($"User:{userId}", 1, ("role", "vendor"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Tag("client_dispatch", "true").Tag("user_id", userId.ToString()).Tag("challenge_id", challengeId)
            .Analyze(new RequiredFieldAnalyzer("code", () => code))
            .Build();

    public static Operation SignOut() =>
        Entry.Create("auth.sign_out")
            .Describe("Vendor signs out")
            .From("User:self", 1, ("role", "vendor"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Build();

    // ── Orders ────────────────────────────────────────────────────────────
    public static Operation AcceptOrder(Guid id) =>
        Entry.Create("vendor.order.accept")
            .Describe($"Vendor accepts Order:{id}")
            .From("User:self", 1, ("role", "vendor"))
            .To($"Order:{id}", 1, ("role", "order"))
            .Tag("client_dispatch", "true").Tag("order_id", id.ToString()).Build();

    public static Operation ReadyOrder(Guid id) =>
        Entry.Create("vendor.order.ready")
            .Describe($"Vendor marks Order:{id} ready")
            .From("User:self", 1, ("role", "vendor"))
            .To($"Order:{id}", 1, ("role", "order"))
            .Tag("client_dispatch", "true").Tag("order_id", id.ToString()).Build();

    public static Operation DeliverOrder(Guid id) =>
        Entry.Create("vendor.order.deliver")
            .Describe($"Vendor delivers Order:{id}")
            .From("User:self", 1, ("role", "vendor"))
            .To($"Order:{id}", 1, ("role", "order"))
            .Tag("client_dispatch", "true").Tag("order_id", id.ToString()).Build();

    public static Operation CancelOrder(Guid id) =>
        Entry.Create("vendor.order.cancel")
            .Describe($"Vendor cancels Order:{id}")
            .From("User:self", 1, ("role", "vendor"))
            .To($"Order:{id}", 1, ("role", "order"))
            .Tag("client_dispatch", "true").Tag("order_id", id.ToString()).Build();

    // ── Offers ────────────────────────────────────────────────────────────
    public static Operation ToggleOffer(Guid id) =>
        Entry.Create("vendor.offer.toggle")
            .Describe($"Vendor toggles Offer:{id}")
            .From("User:self", 1, ("role", "vendor"))
            .To($"Offer:{id}", 1, ("role", "offer"))
            .Tag("client_dispatch", "true").Tag("offer_id", id.ToString()).Build();

    public static Operation DeleteOffer(Guid id) =>
        Entry.Create("vendor.offer.delete")
            .Describe($"Vendor deletes Offer:{id}")
            .From("User:self", 1, ("role", "vendor"))
            .To($"Offer:{id}", 1, ("role", "offer"))
            .Tag("client_dispatch", "true").Tag("offer_id", id.ToString()).Build();

    // ── UI ─────────────────────────────────────────────────────────────────
    public static Operation SetTheme(string theme) =>
        Entry.Create("ui.set_theme").Describe($"Set theme to {theme}")
            .From("User:self", 1, ("role", "vendor")).To("System:ui", 1, ("role", "ui"))
            .Tag("theme", theme).Build();

    public static Operation SetLanguage(string lang) =>
        Entry.Create("ui.set_language").Describe($"Set language to {lang}")
            .From("User:self", 1, ("role", "vendor")).To("System:ui", 1, ("role", "ui"))
            .Tag("language", lang).Build();
}
