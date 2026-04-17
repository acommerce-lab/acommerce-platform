using ACommerce.Client.Http;

namespace Vendor.Web.Operations;

/// <summary>
/// خريطة: نوع العملية → HTTP endpoint.
/// HttpDispatcher يُوجَّه إلى Vendor.Api فقط (المصادقة + الطلبات + الإعدادات).
/// العروض والرسائل تتجه إلى Order.Api عبر OrderApiClient مباشرة.
/// </summary>
public static class VendorRoutes
{
    public static void Register(HttpRouteRegistry routes)
    {
        // Auth (→ Vendor.Api — كل خدمة تُدير مستخدميها محلياً)
        routes.Map("auth.sms.request", HttpMethod.Post, "/api/auth/sms/request");
        routes.Map("auth.sms.verify", HttpMethod.Post, "/api/auth/sms/verify");

        // Vendor Orders (→ Vendor.Api — /api/vendor-orders/*)
        routes.Map("vendor-order.accept",  HttpMethod.Post, "/api/vendor-orders/{order_id}/accept");
        routes.Map("vendor-order.reject",  HttpMethod.Post, "/api/vendor-orders/{order_id}/reject");
        routes.Map("vendor-order.ready",   HttpMethod.Post, "/api/vendor-orders/{order_id}/ready");
        routes.Map("vendor-order.deliver", HttpMethod.Post, "/api/vendor-orders/{order_id}/deliver");
    }
}
