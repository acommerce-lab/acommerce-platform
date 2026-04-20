using ACommerce.Client.Http;

namespace Order.V2.Web.Operations;

public static class OrderRoutes
{
    public static void Register(HttpRouteRegistry routes)
    {
        // Auth
        routes.Map("auth.sms.request", HttpMethod.Post, "/api/auth/sms/request");
        routes.Map("auth.sms.verify", HttpMethod.Post, "/api/auth/sms/verify");

        // Orders
        routes.Map("order.create", HttpMethod.Post, "/api/orders");
        routes.Map("order.cancel", HttpMethod.Post, "/api/orders/{order_id}/cancel");

        // Profile
        routes.Map("profile.update", HttpMethod.Put, "/api/users/{user_id}");

        // Favorites
        routes.Map("favorite.toggle", HttpMethod.Post, "/api/favorites/toggle");

        // Messages
        routes.Map("conversation.start", HttpMethod.Post, "/api/messages/conversations");
        routes.Map("message.send", HttpMethod.Post, "/api/messages");
        routes.Map("conversation.mark_read", HttpMethod.Post, "/api/messages/conversations/{conversation_id}/mark-read");

        // Notifications
        routes.Map("notification.read", HttpMethod.Post, "/api/notifications/{notification_id}/read");
        routes.Map("notification.mark_all_read", HttpMethod.Post, "/api/notifications/user/{user_id}/mark-all-read");
    }
}
