using ACommerce.Client.Http;

namespace Ashare.V2.Provider.Web.Operations;

public static class ProviderV2Routes
{
    public static void Register(HttpRouteRegistry routes)
    {
        // Auth (Nafath)
        routes.Map("auth.nafath.start", HttpMethod.Post, "/auth/nafath/start");

        // Listings (owner management)
        routes.Map("listing.toggle", HttpMethod.Post, "/my-listings/{listing_id}/toggle");

        // Bookings (owner responses)
        routes.Map("booking.confirm", HttpMethod.Post, "/bookings/{booking_id}/confirm");
        routes.Map("booking.reject",  HttpMethod.Post, "/bookings/{booking_id}/reject");
    }
}
