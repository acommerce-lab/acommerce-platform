using ACommerce.Client.Http;
using ACommerce.ClientHost.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Kits.Discovery.Frontend.Customer.Stores;

/// <summary>
/// تَسجيل HTTP routes لِـ Discovery kit. تُستَدعى مِن التَطبيق عِند تَسجيل
/// <c>AddClientOpEngine</c>. HttpDispatcher يَستَخدِمها لِتَحويل
/// <see cref="DiscoveryOps"/> إلى GETs مُناسِبَة.
/// </summary>
public static class DiscoveryRoutesExtensions
{
    public static IServiceCollection AddDiscoveryRoutes(this IServiceCollection services)
    {
        services.AddSingleton<IRoutesRegistrar, DiscoveryRoutesRegistrar>();
        return services;
    }

    /// <summary>
    /// يُسَجِّل <see cref="IDiscoveryStore"/> + <see cref="DefaultDiscoveryStore"/>
    /// كـ scoped. تَطبيقات تُريد تَنفيذاً مُخَصَّصاً (cache مُختَلِف، fallback،
    /// …) تُسَجِّله قَبل هذا الـ extension.
    /// </summary>
    public static IServiceCollection AddDiscoveryStore(this IServiceCollection services)
    {
        services.AddScoped<IDiscoveryStore, DefaultDiscoveryStore>();
        return services;
    }
}

internal sealed class DiscoveryRoutesRegistrar : IRoutesRegistrar
{
    public void Register(HttpRouteRegistry routes)
    {
        routes.Map("discovery.cities.list",     HttpMethod.Get, "/cities");
        routes.Map("discovery.amenities.list",  HttpMethod.Get, "/amenities");
        routes.Map("discovery.categories.list", HttpMethod.Get, "/categories");
    }
}
