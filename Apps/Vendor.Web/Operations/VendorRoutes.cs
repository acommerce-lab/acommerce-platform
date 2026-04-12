using ACommerce.Client.Http;

namespace Vendor.Web.Operations;

/// <summary>
/// خريطة: نوع العملية → HTTP endpoint.
/// HttpDispatcher يستخدم هذه الخريطة لتحويل Entry.Create("auth.sms.request")
/// إلى POST /api/auth/sms/request تلقائياً.
/// جميع العمليات تتجه إلى Vendor.Api (كل خدمة تُدير مستخدميها بنفسها).
/// </summary>
public static class VendorRoutes
{
    public static void Register(HttpRouteRegistry routes)
    {
        // Auth (→ Vendor.Api — الخدمة تُدير مستخدميها محلياً)
        routes.Map("auth.sms.request", HttpMethod.Post, "/api/auth/sms/request");
        routes.Map("auth.sms.verify", HttpMethod.Post, "/api/auth/sms/verify");

        // Offers (→ Order.Api)
        routes.Map("offer.create", HttpMethod.Post, "/api/offers");
        routes.Map("offer.update", HttpMethod.Put, "/api/offers/{offer_id}");

        // Vendor Orders (→ Vendor.Api — routed via VendorApiClient, not HttpDispatcher)
        // These are registered for completeness but mutations go through VendorApiClient directly.
        routes.Map("vendor-order.accept", HttpMethod.Post, "/api/orders/{order_id}/accept");
        routes.Map("vendor-order.reject", HttpMethod.Post, "/api/orders/{order_id}/cancel");
        routes.Map("vendor-order.ready", HttpMethod.Post, "/api/orders/{order_id}/ready");
        routes.Map("vendor-order.deliver", HttpMethod.Post, "/api/orders/{order_id}/deliver");
    }
}
