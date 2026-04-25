using ACommerce.Client.Http;

namespace Ejar.Provider.Web.Operations;

public static class EjarRoutes
{
    public static void Register(HttpRouteRegistry r)
    {
        // Auth
        r.Map("auth.otp.request",        HttpMethod.Post,   "/auth/otp/request");
        r.Map("auth.otp.verify",         HttpMethod.Post,   "/auth/otp/verify");
        r.Map("auth.logout",             HttpMethod.Post,   "/auth/logout");

        // Listings (owner)
        r.Map("listing.toggle",          HttpMethod.Post,   "/my-listings/{listing_id}/toggle");
        r.Map("listing.create",          HttpMethod.Post,   "/my-listings");
        r.Map("listing.delete",          HttpMethod.Delete, "/my-listings/{listing_id}");

        // Favorites
        r.Map("favorite.toggle",         HttpMethod.Post,   "/listings/{listing_id}/favorite");

        // Conversations + Messages
        r.Map("conversation.start",      HttpMethod.Post,   "/conversations/start");
        r.Map("message.send",            HttpMethod.Post,   "/conversations/{conversation_id}/messages");

        // Notifications
        r.Map("notification.read",       HttpMethod.Post,   "/notifications/{notification_id}/read");
        r.Map("notification.read.all",   HttpMethod.Post,   "/notifications/read-all");

        // Complaints
        r.Map("complaint.file",          HttpMethod.Post,   "/complaints");
        r.Map("complaint.reply",         HttpMethod.Post,   "/complaints/{complaint_id}/replies");

        // Profile
        r.Map("profile.update",          HttpMethod.Put,    "/me/profile");
    }
}
