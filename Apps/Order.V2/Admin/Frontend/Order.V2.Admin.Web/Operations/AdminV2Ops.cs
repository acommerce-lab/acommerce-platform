using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace Order.V2.Admin.Web.Operations;

public static class AdminV2Ops
{
    // ── Auth ──────────────────────────────────────────────────────────────
    public static Operation RequestOtp(string phone) =>
        Entry.Create("auth.sms.request")
            .Describe($"Admin requests OTP for {phone}")
            .From("User:anonymous", 1, ("role", "requester"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Tag("client_dispatch", "true").Tag("phone_number", phone)
            .Analyze(new RequiredFieldAnalyzer("phone", () => phone))
            .Build();

    public static Operation VerifyOtp(Guid userId, string challengeId, string code) =>
        Entry.Create("auth.sms.verify")
            .Describe($"Admin verifies OTP for User:{userId}")
            .From($"User:{userId}", 1, ("role", "admin"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Tag("client_dispatch", "true").Tag("user_id", userId.ToString()).Tag("challenge_id", challengeId)
            .Analyze(new RequiredFieldAnalyzer("code", () => code))
            .Build();

    public static Operation SignOut() =>
        Entry.Create("auth.sign_out")
            .Describe("Admin signs out")
            .From("User:self", 1, ("role", "admin"))
            .To("System:auth", 1, ("role", "auth_service"))
            .Build();

    // ── User actions ──────────────────────────────────────────────────────
    public static Operation SuspendUser(Guid id) =>
        Entry.Create("admin.user.suspend")
            .Describe($"Admin suspends User:{id}")
            .From("User:self", 1, ("role", "admin"))
            .To($"User:{id}", 1, ("role", "target"))
            .Tag("client_dispatch", "true").Tag("user_id", id.ToString()).Build();

    public static Operation ActivateUser(Guid id) =>
        Entry.Create("admin.user.activate")
            .Describe($"Admin activates User:{id}")
            .From("User:self", 1, ("role", "admin"))
            .To($"User:{id}", 1, ("role", "target"))
            .Tag("client_dispatch", "true").Tag("user_id", id.ToString()).Build();

    // ── Vendor actions ─────────────────────────────────────────────────────
    public static Operation SuspendVendor(Guid id) =>
        Entry.Create("admin.vendor.suspend")
            .Describe($"Admin suspends Vendor:{id}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Vendor:{id}", 1, ("role", "vendor"))
            .Tag("client_dispatch", "true").Tag("vendor_id", id.ToString()).Build();

    public static Operation ActivateVendor(Guid id) =>
        Entry.Create("admin.vendor.activate")
            .Describe($"Admin activates Vendor:{id}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Vendor:{id}", 1, ("role", "vendor"))
            .Tag("client_dispatch", "true").Tag("vendor_id", id.ToString()).Build();

    // ── Offer actions ──────────────────────────────────────────────────────
    public static Operation DeactivateOffer(Guid id) =>
        Entry.Create("admin.offer.deactivate")
            .Describe($"Admin deactivates Offer:{id}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Offer:{id}", 1, ("role", "offer"))
            .Tag("client_dispatch", "true").Tag("offer_id", id.ToString()).Build();

    // ── UI ─────────────────────────────────────────────────────────────────
    public static Operation SetTheme(string theme) =>
        Entry.Create("ui.set_theme").Describe($"Set theme to {theme}")
            .From("User:self", 1, ("role", "admin")).To("System:ui", 1, ("role", "ui"))
            .Tag("theme", theme).Build();

    public static Operation SetLanguage(string lang) =>
        Entry.Create("ui.set_language").Describe($"Set language to {lang}")
            .From("User:self", 1, ("role", "admin")).To("System:ui", 1, ("role", "ui"))
            .Tag("language", lang).Build();
}
