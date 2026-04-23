using ACommerce.Client.Http;

namespace Ashare.V2.Admin.Web.Operations;

public static class AdminV2Routes
{
    public static void Register(HttpRouteRegistry r)
    {
        r.Map("auth.admin.sms.request", HttpMethod.Post, "/api/auth/sms/request");
        r.Map("auth.admin.sms.verify",  HttpMethod.Post, "/api/auth/sms/verify");
        r.Map("admin.user.suspend",     HttpMethod.Post, "/api/admin/users/{user_id}/suspend");
        r.Map("admin.user.activate",    HttpMethod.Post, "/api/admin/users/{user_id}/activate");
        r.Map("admin.listing.publish",  HttpMethod.Post, "/api/admin/listings/{listing_id}/publish");
        r.Map("admin.listing.reject",   HttpMethod.Post, "/api/admin/listings/{listing_id}/reject");
        r.Map("admin.listing.delete",   HttpMethod.Delete, "/api/admin/listings/{listing_id}");
        r.Map("admin.category.create",  HttpMethod.Post, "/api/admin/categories");
        r.Map("admin.category.delete",  HttpMethod.Delete, "/api/admin/categories/{category_id}");
        r.Map("ui.set_theme",           HttpMethod.Post, "/api/ui/theme");
        r.Map("ui.set_language",        HttpMethod.Post, "/api/ui/language");
        r.Map("auth.sign_out",          HttpMethod.Post, "/api/auth/sign-out");
    }
}
