using ACommerce.Compositions.Customer.Marketplace.Home.Backend;
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.ServiceHost;

public static class MarketplaceHomeBackendModule
{
    /// <summary>
    /// يُسَجِّل Customer Marketplace Home composition + يَضُمّ
    /// <see cref="MarketplaceHomeController"/> إلى Application Parts.
    ///
    /// <code>
    /// host.UseMarketplaceHomeBackend&lt;EjarHomeListingsSource,
    ///                                  EjarHomeListingProjection,
    ///                                  EjarDiscoveryCategoryProvider&gt;();
    /// </code>
    ///
    /// <para>اقتِراحات البَحث (<see cref="IHomeSearchSuggestions"/>) +
    /// صَفحات قانونِيَّة (<see cref="ILegalPageProvider"/>) لَهُما افتِراضات
    /// — التَطبيق يُسَجِّل override بِـ <c>AddSingleton</c> صَريح إذا أَراد.</para>
    /// </summary>
    public static ServiceHostBuilder UseMarketplaceHomeBackend
        <TSource, TProjection, TCategoryProvider>(this ServiceHostBuilder host)
        where TSource           : class, IHomeListingsSource
        where TProjection       : class, IHomeListingProjection
        where TCategoryProvider : class, IDiscoveryCategoryProvider
    {
        host.Builder.Services.AddMarketplaceHomeBackend<TSource, TProjection, TCategoryProvider>();
        host.ExtraControllerAssemblies.Add(typeof(MarketplaceHomeController).Assembly);
        return host;
    }
}
