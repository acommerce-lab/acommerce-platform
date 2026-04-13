using ACommerce.Client.Http;

namespace Order.Admin.Web.Operations;

public static class AdminRoutes
{
    public static void Register(HttpRouteRegistry routes)
    {
        routes.Map("auth.sms.request", HttpMethod.Post, "/api/auth/sms/request");
        routes.Map("auth.sms.verify", HttpMethod.Post, "/api/auth/sms/verify");
        routes.Map("admin.user.suspend", HttpMethod.Post, "/api/admin/users/{user_id}/suspend");
        routes.Map("admin.user.activate", HttpMethod.Post, "/api/admin/users/{user_id}/activate");
        routes.Map("admin.vendor.approve", HttpMethod.Post, "/api/admin/vendors/{vendor_id}/approve");
        routes.Map("admin.vendor.suspend", HttpMethod.Post, "/api/admin/vendors/{vendor_id}/suspend");
        routes.Map("admin.order.cancel", HttpMethod.Post, "/api/admin/orders/{order_id}/cancel");
        routes.Map("admin.order.refund", HttpMethod.Post, "/api/admin/orders/{order_id}/refund");
        routes.Map("admin.offer.approve", HttpMethod.Post, "/api/admin/offers/{offer_id}/approve");
        routes.Map("admin.offer.suspend", HttpMethod.Post, "/api/admin/offers/{offer_id}/suspend");
        routes.Map("admin.offer.delete", HttpMethod.Delete, "/api/admin/offers/{offer_id}");
        routes.Map("admin.category.create", HttpMethod.Post, "/api/admin/categories");
        routes.Map("admin.category.delete", HttpMethod.Delete, "/api/admin/categories/{category_id}");
    }
}
