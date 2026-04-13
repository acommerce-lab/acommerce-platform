using ACommerce.Client.Http;

namespace Ashare.Provider.Web.Operations;

public static class ProviderRoutes
{
    public static void Register(HttpRouteRegistry routes)
    {
        // Auth
        routes.Map("auth.sms.request", HttpMethod.Post, "/api/auth/sms/request");
        routes.Map("auth.sms.verify", HttpMethod.Post, "/api/auth/sms/verify");

        // Listings (owner management)
        routes.Map("listing.create", HttpMethod.Post, "/api/listings");
        routes.Map("listing.update", HttpMethod.Put, "/api/listings/{listing_id}");
        routes.Map("listing.delete", HttpMethod.Delete, "/api/listings/{listing_id}");
        routes.Map("listing.feature", HttpMethod.Post, "/api/listings/{listing_id}/feature");

        // Bookings (owner responses)
        routes.Map("booking.confirm", HttpMethod.Post, "/api/bookings/{booking_id}/confirm");
        routes.Map("booking.reject", HttpMethod.Post, "/api/bookings/{booking_id}/reject");

        // Profile
        routes.Map("profile.upsert", HttpMethod.Put, "/api/profiles");

        // Subscriptions
        routes.Map("subscription.create", HttpMethod.Post, "/api/subscriptions");
        routes.Map("subscription.cancel", HttpMethod.Post, "/api/subscriptions/{subscription_id}/cancel");
    }
}
