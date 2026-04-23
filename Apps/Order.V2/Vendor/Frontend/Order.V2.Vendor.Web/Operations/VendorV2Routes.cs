using ACommerce.Client.Http;

namespace Order.V2.Vendor.Web.Operations;

public static class VendorV2Routes
{
    public static void Register(HttpRouteRegistry routes)
    {
        routes.Map("auth.sms.request", HttpMethod.Post, "/api/vendor/auth/sms/request");
        routes.Map("auth.sms.verify",  HttpMethod.Post, "/api/vendor/auth/sms/verify");

        routes.Map("vendor.order.accept",  HttpMethod.Post, "/api/vendor/orders/{order_id}/accept");
        routes.Map("vendor.order.ready",   HttpMethod.Post, "/api/vendor/orders/{order_id}/ready");
        routes.Map("vendor.order.deliver", HttpMethod.Post, "/api/vendor/orders/{order_id}/deliver");
        routes.Map("vendor.order.cancel",  HttpMethod.Post, "/api/vendor/orders/{order_id}/cancel");

        routes.Map("vendor.offer.toggle", HttpMethod.Post,   "/api/vendor/offers/{offer_id}/toggle");
        routes.Map("vendor.offer.delete", HttpMethod.Delete, "/api/vendor/offers/{offer_id}");
    }
}
