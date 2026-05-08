using ACommerce.Templates.Customer.Marketplace;
using Ejar.Customer.UI.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Ejar.Customer.UI.ClientHost;

/// <summary>
/// V1 thin host — مُطابِق لِشَكل V2 تَماماً، يَختَلِف فقط في:
/// <list type="bullet">
///   <item>القالَب: <c>Customer.Marketplace</c> (V2 يَستَخدِم <c>Customer.Ledger</c>).</item>
///   <item>اسم الـ scheme + storage: <c>EjarV1Auth</c> / <c>ejar.v1.auth</c>.</item>
///   <item>الألوان: <c>wwwroot/branding.css</c> في Ejar.Web.</item>
/// </list>
/// </summary>
public static class EjarCustomerHostExtensions
{
    public static IServiceCollection AddEjarCustomer(this IServiceCollection services)
    {
        services.AddTemplate_Customer_Marketplace(o =>
        {
            o.HttpClientName = "ejar";
            o.StorageKey     = "ejar.v1.auth";
            o.Scheme         = "EjarV1Auth";

            o.UrlAllowlist.Add("cdn.ejar.sa");
            o.UrlAllowlist.Add("storage.googleapis.com");
            o.UrlAllowlist.Add("firebasestorage.googleapis.com");

            // Ejar V1 يَستَعمِل /properties بَدَل /listings — نَستَبعِد الافتراضيّين
            // ونُسَجِّل /properties مَع نَفس الـ widgets.
            o.ExcludedRoutes.Add("listings");
            o.ExcludedRoutes.Add("listing-details");

            // صَفحَتان خاصّتان بالتَطبيق
            o.ExtraPages.Add(("/",          typeof(EjarHomeWidget),      false));
            o.ExtraPages.Add(("/dashboard", typeof(EjarDashboardWidget), true));
        });

        return services;
    }
}
