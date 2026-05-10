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
        return services;
    }
}
