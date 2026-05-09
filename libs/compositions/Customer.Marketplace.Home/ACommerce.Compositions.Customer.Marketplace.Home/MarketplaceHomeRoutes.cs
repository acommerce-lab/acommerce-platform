using ACommerce.Client.Http;
using ACommerce.ClientHost.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Compositions.Customer.Marketplace.Home;

/// <summary>
/// تَسجيل HTTP routes لِـ composition Customer.Marketplace.Home.
/// HttpDispatcher يَستَخدِمها لِتَحويل <see cref="MarketplaceHomeOps"/> إلى
/// GETs مُناسِبَة (qs.* tags تَنتَقِل query string تلقائيّاً).
/// </summary>
public static class MarketplaceHomeServiceCollectionExtensions
{
    /// <summary>
    /// يُسَجِّل routes + <see cref="IMarketplaceHomeStore"/> + <see cref="DefaultMarketplaceHomeStore"/>.
    /// تَطبيقات تُريد تَنفيذاً مُخَصَّصاً تُسَجِّله قَبل هذا الـ extension.
    /// </summary>
    public static IServiceCollection AddCustomerMarketplaceHomeComposition(this IServiceCollection services)
    {
        services.AddSingleton<IRoutesRegistrar, MarketplaceHomeRoutesRegistrar>();
        services.AddScoped<IMarketplaceHomeStore, DefaultMarketplaceHomeStore>();
        return services;
    }
}

internal sealed class MarketplaceHomeRoutesRegistrar : IRoutesRegistrar
{
    public void Register(HttpRouteRegistry routes)
    {
        routes.Map("home.view",               HttpMethod.Get, "/home/view");
        routes.Map("home.explore",            HttpMethod.Get, "/home/explore");
        routes.Map("home.search.suggestions", HttpMethod.Get, "/home/search/suggestions");
        routes.Map("legal.list",              HttpMethod.Get, "/legal");
    }
}
