using ACommerce.Client.Http;

namespace Ashare.V2.Web.Operations;

/// <summary>
/// ربط نوع العمليّة ← HTTP endpoint على Ashare.V2.Api.
/// HttpDispatcher يعتمد على هذه الخريطة ليحوّل <c>Entry.Create("booking.create")</c>
/// إلى <c>POST /bookings</c> تلقائيّاً. قوالب <c>{tag_name}</c> تُستبدَل من tags العمليّة.
/// </summary>
public static class V2Routes
{
    public static void Register(HttpRouteRegistry r)
    {
        // Auth
        r.Map("auth.nafath.start",       HttpMethod.Post, "/auth/nafath/start");

        // Listings (owner actions — interceptor-enforced must_own)
        r.Map("listing.toggle",          HttpMethod.Post, "/my-listings/{listing_id}/toggle");

        // Bookings (must_not_own on target listing)
        r.Map("booking.create",          HttpMethod.Post, "/bookings");

        // Conversations + Messages
        r.Map("conversation.start",      HttpMethod.Post, "/conversations/start");
        r.Map("message.send",            HttpMethod.Post, "/conversations/{conversation_id}/messages");

        // Notifications
        r.Map("notification.read",       HttpMethod.Post, "/home/notifications/{notification_id}/read");
        r.Map("notification.read.all",   HttpMethod.Post, "/home/notifications/read-all");

        // Complaints
        r.Map("complaint.file",          HttpMethod.Post, "/complaints");
        r.Map("complaint.reply",         HttpMethod.Post, "/complaints/{complaint_id}/replies");

        // Profile
        r.Map("profile.update",          HttpMethod.Put,  "/me/profile");
    }
}
