using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace Ashare.V2.Admin.Web.Operations;

public static class AdminV2Ops
{
    public static Operation RequestOtp(string phone) =>
        Entry.Create("auth.admin.sms.request")
            .From("User:anonymous", 1, ("role", "requester"))
            .To("System:auth", 1, ("role", "auth"))
            .Tag("client_dispatch", "true")
            .Analyze(new RequiredFieldAnalyzer("phone", () => phone))
            .Build();

    public static Operation VerifyOtp(Guid userId, string challengeId, string code) =>
        Entry.Create("auth.admin.sms.verify")
            .From($"User:{userId}", 1, ("role", "user"))
            .To("System:auth", 1, ("role", "auth"))
            .Tag("client_dispatch", "true")
            .Analyze(new RequiredFieldAnalyzer("code", () => code))
            .Build();

    public static Operation SignOut() =>
        Entry.Create("auth.sign_out")
            .From("User:self", 1, ("role", "user"))
            .To("System:auth", 1, ("role", "auth"))
            .Build();

    public static Operation SetTheme(string theme) =>
        Entry.Create("ui.set_theme")
            .From("User:self", 1, ("role", "user"))
            .To("System:ui", 1, ("role", "ui"))
            .Tag("theme", theme)
            .Build();

    public static Operation SetLanguage(string lang) =>
        Entry.Create("ui.set_language")
            .From("User:self", 1, ("role", "user"))
            .To("System:ui", 1, ("role", "ui"))
            .Tag("language", lang)
            .Build();

    public static Operation SuspendUser(Guid userId) =>
        Entry.Create("admin.user.suspend")
            .From("Admin:system", 1, ("role", "admin"))
            .To($"User:{userId}", 1, ("role", "user"))
            .Tag("client_dispatch", "true").Tag("user_id", userId.ToString())
            .Build();

    public static Operation ActivateUser(Guid userId) =>
        Entry.Create("admin.user.activate")
            .From("Admin:system", 1, ("role", "admin"))
            .To($"User:{userId}", 1, ("role", "user"))
            .Tag("client_dispatch", "true").Tag("user_id", userId.ToString())
            .Build();

    public static Operation PublishListing(Guid listingId) =>
        Entry.Create("admin.listing.publish")
            .From("Admin:system", 1, ("role", "admin"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true").Tag("listing_id", listingId.ToString())
            .Build();

    public static Operation RejectListing(Guid listingId) =>
        Entry.Create("admin.listing.reject")
            .From("Admin:system", 1, ("role", "admin"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true").Tag("listing_id", listingId.ToString())
            .Build();

    public static Operation DeleteListing(Guid listingId) =>
        Entry.Create("admin.listing.delete")
            .From("Admin:system", 1, ("role", "admin"))
            .To($"Listing:{listingId}", 1, ("role", "listing"))
            .Tag("client_dispatch", "true").Tag("listing_id", listingId.ToString())
            .Build();
}
