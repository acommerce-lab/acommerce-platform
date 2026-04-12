using ACommerce.Client.Http;

namespace Ashare.Web.Operations;

/// <summary>
/// خريطة: نوع العملية → HTTP endpoint في Ashare.Api.
/// HttpDispatcher يستخدم هذه الخريطة لتحويل Entry.Create("auth.sms.request")
/// إلى POST /api/auth/sms/request تلقائياً.
/// جميع العمليات تتجه إلى Ashare.Api (منفردة - كل خدمة تُدير نفسها).
/// </summary>
public static class AshareRoutes
{
    public static void Register(HttpRouteRegistry routes)
    {
        // Auth (→ Ashare.Api — مستقلة، تُدير مستخدميها محلياً)
        routes.Map("auth.sms.request", HttpMethod.Post, "/api/auth/sms/request");
        routes.Map("auth.sms.verify", HttpMethod.Post, "/api/auth/sms/verify");

        // Listings
        routes.Map("listing.create", HttpMethod.Post, "/api/listings");

        // Bookings
        routes.Map("booking.create", HttpMethod.Post, "/api/bookings");
        routes.Map("booking.cancel", HttpMethod.Post, "/api/bookings/{booking_id}/cancel");
        routes.Map("booking.pay", HttpMethod.Post, "/api/bookings/{booking_id}/pay");

        // Subscriptions
        routes.Map("subscription.create", HttpMethod.Post, "/api/subscriptions");

        // Messages
        routes.Map("message.send", HttpMethod.Post, "/api/messages");

        // Notifications
        routes.Map("notification.read", HttpMethod.Post, "/api/notifications/{notification_id}/read");
        routes.Map("notification.mark_all_read", HttpMethod.Post, "/api/notifications/user/{user_id}/mark-all-read");
        routes.Map("notification.send_test", HttpMethod.Post, "/api/notifications/send");

        // Conversations
        routes.Map("conversation.start", HttpMethod.Post, "/api/messages/conversations");

        // Payments
        routes.Map("payment.initiate", HttpMethod.Post, "/api/payments/initiate");
        routes.Map("payment.simulate_callback", HttpMethod.Post, "/api/payments/callback/{payment_id}");
    }
}
