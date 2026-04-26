using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Bookings.Operations.Extensions;

/// <summary>DI registration for booking operations.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBookingOperations(this IServiceCollection services)
    {
        services.AddScoped(typeof(BookingService<>));
        return services;
    }
}
