using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ACommerce.Compositions.Customer.Marketplace.Home.Backend;

/// <summary>
/// تَسجيل DI لِـ <c>/home/*</c> + <c>/legal</c> في تَطبيقات Customer Marketplace.
/// التَطبيق يَستَدعي <c>AddMarketplaceHomeBackend&lt;TSource, TProjection, TProvider&gt;()</c>
/// ويُمَرِّر impls الخاصَّة بِه. اقتِراحات البَحث وَ صَفحات القانون لَهُما
/// افتِراضات؛ التَطبيق يَستَطيع override بِـ <c>AddSingleton</c> صَريح.
/// </summary>
public static class MarketplaceHomeBackendCompositionExtensions
{
    /// <summary>
    /// يُسَجِّل الـ ports الإلزامِيَّة (Source + Projection + CategoryProvider)
    /// + يَضمَن وُجود افتِراضات لِـ Suggestions + Legal. الـ Controller
    /// يُلتَقَط تِلقائيّاً عَبر ApplicationPart عَلى assembly هذه المَكتَبَة.
    /// </summary>
    public static IServiceCollection AddMarketplaceHomeBackend
        <TSource, TProjection, TCategoryProvider>(this IServiceCollection services)
        where TSource           : class, IHomeListingsSource
        where TProjection       : class, IHomeListingProjection
        where TCategoryProvider : class, IDiscoveryCategoryProvider
    {
        services.AddScoped<IHomeListingsSource,        TSource>();
        services.AddScoped<IHomeListingProjection,     TProjection>();
        services.AddScoped<IDiscoveryCategoryProvider, TCategoryProvider>();

        // التَطبيق قَد يَستَبدِل أَيّاً مَن هذَين بِـ AddSingleton<IHomeSearchSuggestions,
        // MySuggestions>() قَبل/بَعد هذا الاستِدعاء.
        services.TryAddSingleton<IHomeSearchSuggestions, EmptyHomeSearchSuggestions>();
        services.TryAddSingleton<ILegalPageProvider,     DefaultLegalPageProvider>();

        return services;
    }
}
