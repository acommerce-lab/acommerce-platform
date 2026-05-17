using ACommerce.ClientHost.Auth;
using ACommerce.Culture.Abstractions;
using ACommerce.Kits.DynamicAttributes.Frontend.Customer.Stores;
using ACommerce.Kits.Taxonomy.Frontend.Customer.Stores;
using ACommerce.L10n.Blazor;
using Ejar.Customer.UI.ClientHost;
using Microsoft.Extensions.DependencyInjection;

namespace Ashare.V3.Customer.UI.ClientHost;

/// <summary>
/// Ashare V3 thin host — يُفَوِّض بِكامِله لِـ Ejar V1 host
/// (<see cref="EjarCustomerHostExtensions.AddEjarCustomer"/>) ثُمّ يُضيف
/// طَبَقَة تَرجَمات Ashare V3 فَوقها لِيَفوز عَلى <c>app.name</c> + بَعض
/// مَفاتيح Home الخاصّة بِالسَكَن المُشتَرَك.
///
/// <para>التَّخصيص البَصَريّ يَأتي مِن <c>branding.css</c> في <c>Ashare.V3.Web/wwwroot/</c>
/// (أَلوان عَشير الزَيتونيّ + البُرتُقاليّ).</para>
/// </summary>
public static class AshareV3CustomerHostExtensions
{
    public static IServiceCollection AddAshareV3Customer(this IServiceCollection services)
    {
        services.AddEjarCustomer();          // قالَب Customer.Marketplace + V1 wiring + Ejar translations
        services.AddAshareV3Translations();  // طَبَقَة Ashare فَوقَها (تَفوز)

        // DynamicAttributes + Taxonomy stores — يَجِب أَن نَحقُن
        // <c>AuthenticatedHttpClient.Client</c> صَراحَةً لِيَأخُذ BaseAddress + Bearer.
        // <c>EjarCustomerHostExtensions.AddEjarCustomer</c> سَجَّلَها لَكِنّنا نُعيد
        // التَّسجيل هُنا لِيَكون التَّبَنّي صَريحاً عَلى مُستَوى V3 (لا يَعتَمِد
        // عَلى تَفاصيل V1).
        services.AddScoped<IAttributesStore>(sp =>
            new HttpAttributesStore(sp.GetRequiredService<AuthenticatedHttpClient>().Client));
        services.AddScoped<ITaxonomyStore>(sp =>
            new HttpTaxonomyStore(sp.GetRequiredService<AuthenticatedHttpClient>().Client));

        // ICultureContext + ILanguageContext سُعودِيَّة — تَفوز عَلى تَسجيل
        // قالَب Customer.Marketplace اليَمَني (Asia/Aden + YER).
        services.AddScoped<ICultureContext, AshareV3CultureContext>();
        services.AddScoped<ILanguageContext, AshareV3CultureContext>();
        return services;
    }
}
