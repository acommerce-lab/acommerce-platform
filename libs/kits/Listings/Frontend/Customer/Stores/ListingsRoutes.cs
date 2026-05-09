using ACommerce.Client.Http;
using ACommerce.ClientHost.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Listings.Frontend.Customer.Stores;

public static class ListingsRoutesExtensions
{
    public static IServiceCollection AddListingsRoutes(this IServiceCollection services)
    {
        services.AddSingleton<IRoutesRegistrar, ListingsRoutesRegistrar>();
        return services;
    }

    /// <summary>
    /// يُسَجِّل <see cref="IListingDraft"/> كـ scoped (واحِد لِكلّ Blazor circuit)
    /// مَع تَنفيذ <see cref="DefaultListingDraft"/>. تَطبيقات تُريد persistence
    /// تُسَجِّل تَنفيذاً مُخَصَّصاً قَبل هذا الـ extension.
    /// </summary>
    public static IServiceCollection AddListingDraft(this IServiceCollection services)
    {
        services.AddScoped<IListingDraft, DefaultListingDraft>();
        return services;
    }
}

internal sealed class ListingsRoutesRegistrar : IRoutesRegistrar
{
    public void Register(HttpRouteRegistry routes)
    {
        routes.Map("listings.search",    HttpMethod.Get,    "/listings");
        routes.Map("listings.get",       HttpMethod.Get,    "/listings/{id}");
        routes.Map("listings.list_mine", HttpMethod.Get,    "/my-listings");
        routes.Map("listings.create",    HttpMethod.Post,   "/my-listings");
        routes.Map("listings.toggle",    HttpMethod.Post,   "/my-listings/{id}/toggle");
        routes.Map("listings.delete",    HttpMethod.Delete, "/my-listings/{id}");
    }
}
