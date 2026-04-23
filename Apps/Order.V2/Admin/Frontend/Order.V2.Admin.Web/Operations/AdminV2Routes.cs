using ACommerce.Client.Http;

namespace Order.V2.Admin.Web.Operations;

public static class AdminV2Routes
{
    public static void Register(HttpRouteRegistry routes)
    {
        routes.Map("auth.sms.request", HttpMethod.Post, "/api/admin/auth/sms/request");
        routes.Map("auth.sms.verify",  HttpMethod.Post, "/api/admin/auth/sms/verify");

        routes.Map("admin.user.suspend",  HttpMethod.Post, "/api/admin/users/{user_id}/suspend");
        routes.Map("admin.user.activate", HttpMethod.Post, "/api/admin/users/{user_id}/activate");

        routes.Map("admin.vendor.suspend",  HttpMethod.Post, "/api/admin/vendors/{vendor_id}/suspend");
        routes.Map("admin.vendor.activate", HttpMethod.Post, "/api/admin/vendors/{vendor_id}/activate");

        routes.Map("admin.offer.deactivate", HttpMethod.Post, "/api/admin/offers/{offer_id}/deactivate");
    }
}
