using ACommerce.Templates.Customer.Ledger;
using Ejar.Customer.UI.V2.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Ejar.Customer.UI.V2.ClientHost;

/// <summary>
/// V2 thin host — يَختزِل التَطبيق إلى ٤ أَشياء:
/// <list type="bullet">
///   <item>اختيار قالَب: <c>AddTemplate_Customer_Ledger</c>.</item>
///   <item>إعدادات Auth الخاصّة بالتَطبيق (HttpClient/Storage/Scheme).</item>
///   <item>قائمة allowlist للـ external URLs.</item>
///   <item>صَفحَتان مَخصَّصَتان (Home، Dashboard) كـ ExtraPages.</item>
/// </list>
///
/// <para>التَطبيق غير مُقَيَّد بالقالَب: لو احتاج Realtime chat مَثلاً،
/// يُسَجِّل <c>RealtimeChatStore</c> ثُمّ يَضَع <c>StoreOverrides["chat"] =
/// typeof(RealtimeChatStore)</c>. لو لا يُريد <c>/chat</c> أَصلاً يُضيف
/// <c>"chat"</c> و <c>"chat-room"</c> في <c>ExcludedRoutes</c>.</para>
/// </summary>
public static class EjarV2CustomerHostExtensions
{
    public static IServiceCollection AddEjarCustomerV2(this IServiceCollection services)
    {
        services.AddTemplate_Customer_Ledger(o =>
        {
            o.HttpClientName = "ejar";
            o.StorageKey     = "ejar.v2.auth";
            o.Scheme         = "EjarV2Auth";

            o.UrlAllowlist.Add("cdn.ejar.sa");
            o.UrlAllowlist.Add("storage.googleapis.com");
            o.UrlAllowlist.Add("firebasestorage.googleapis.com");

            // صَفحَتان خاصّتان بالتَطبيق — لا تَنتمي لكيت ولا لقالَب.
            o.ExtraPages.Add(("/",          typeof(EjarHomeWidget),      false));
            o.ExtraPages.Add(("/dashboard", typeof(EjarDashboardWidget), true));
        });

        return services;
    }
}
