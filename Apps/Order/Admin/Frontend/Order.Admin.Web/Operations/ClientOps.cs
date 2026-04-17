using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace Order.Admin.Web.Operations;

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

    // ── Admin Vendor Actions ──────────────────────────────────────────────
    public static Operation ApproveVendor(Guid vendorId) =>
        Entry.Create("admin.vendor.approve")
            .Describe($"Admin approves Vendor:{vendorId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Vendor:{vendorId}", 1, ("role", "vendor"))
            .Tag("client_dispatch", "true")
            .Tag("vendor_id", vendorId.ToString())
            .Build();

    public static Operation SuspendVendor(Guid vendorId) =>
        Entry.Create("admin.vendor.suspend")
            .Describe($"Admin suspends Vendor:{vendorId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Vendor:{vendorId}", 1, ("role", "vendor"))
            .Tag("client_dispatch", "true")
            .Tag("vendor_id", vendorId.ToString())
            .Build();

    // ── Admin Order Actions ───────────────────────────────────────────────
    public static Operation CancelOrder(Guid orderId) =>
        Entry.Create("admin.order.cancel")
            .Describe($"Admin cancels Order:{orderId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Order:{orderId}", 1, ("role", "order"))
            .Tag("client_dispatch", "true")
            .Tag("order_id", orderId.ToString())
            .Build();

    public static Operation RefundOrder(Guid orderId) =>
        Entry.Create("admin.order.refund")
            .Describe($"Admin refunds Order:{orderId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Order:{orderId}", 1, ("role", "order"))
            .Tag("client_dispatch", "true")
            .Tag("order_id", orderId.ToString())
            .Build();

    // ── Admin Offer Actions ───────────────────────────────────────────────
    public static Operation ApproveOffer(Guid offerId) =>
        Entry.Create("admin.offer.approve")
            .Describe($"Admin approves Offer:{offerId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Offer:{offerId}", 1, ("role", "offer"))
            .Tag("client_dispatch", "true")
            .Tag("offer_id", offerId.ToString())
            .Build();

    public static Operation SuspendOffer(Guid offerId) =>
        Entry.Create("admin.offer.suspend")
            .Describe($"Admin suspends Offer:{offerId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Offer:{offerId}", 1, ("role", "offer"))
            .Tag("client_dispatch", "true")
            .Tag("offer_id", offerId.ToString())
            .Build();

    public static Operation DeleteOffer(Guid offerId) =>
        Entry.Create("admin.offer.delete")
            .Describe($"Admin deletes Offer:{offerId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Offer:{offerId}", 1, ("role", "offer"))
            .Tag("client_dispatch", "true")
            .Tag("offer_id", offerId.ToString())
            .Build();

    // ── Admin Category Actions ────────────────────────────────────────────
    public static Operation CreateCategory(string nameAr, string nameEn) =>
        Entry.Create("admin.category.create")
            .Describe("Admin creates a new category")
            .From("User:self", 1, ("role", "admin"))
            .To("System:categories", 1, ("role", "category_service"))
            .Tag("client_dispatch", "true")
            .Tag("name_ar", nameAr)
            .Tag("name_en", nameEn)
            .Analyze(new RequiredFieldAnalyzer("name_ar", () => nameAr))
            .Analyze(new RequiredFieldAnalyzer("name_en", () => nameEn))
            .Build();

    public static Operation DeleteCategory(Guid categoryId) =>
        Entry.Create("admin.category.delete")
            .Describe($"Admin deletes Category:{categoryId}")
            .From("User:self", 1, ("role", "admin"))
            .To($"Category:{categoryId}", 1, ("role", "category"))
            .Tag("client_dispatch", "true")
            .Tag("category_id", categoryId.ToString())
            .Build();
}
