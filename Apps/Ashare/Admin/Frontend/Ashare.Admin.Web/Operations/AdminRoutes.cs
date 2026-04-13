using ACommerce.Client.Http;

namespace Ashare.Admin.Web.Operations;

public static class AdminRoutes
{
    public static void Register(HttpRouteRegistry routes)
    {
        routes.Map("auth.sms.request", HttpMethod.Post, "/api/auth/sms/request");
        routes.Map("auth.sms.verify", HttpMethod.Post, "/api/auth/sms/verify");
        routes.Map("admin.user.suspend", HttpMethod.Post, "/api/admin/users/{user_id}/suspend");
        routes.Map("admin.user.activate", HttpMethod.Post, "/api/admin/users/{user_id}/activate");
        routes.Map("admin.listing.approve", HttpMethod.Post, "/api/admin/listings/{listing_id}/approve");
        routes.Map("admin.listing.reject", HttpMethod.Post, "/api/admin/listings/{listing_id}/reject");
        routes.Map("admin.listing.feature", HttpMethod.Post, "/api/admin/listings/{listing_id}/feature");
        routes.Map("admin.listing.delete", HttpMethod.Delete, "/api/admin/listings/{listing_id}");
        routes.Map("admin.subscription.cancel", HttpMethod.Post, "/api/admin/subscriptions/{subscription_id}/cancel");
        routes.Map("admin.notification.broadcast", HttpMethod.Post, "/api/admin/notifications/broadcast");
    }
}
