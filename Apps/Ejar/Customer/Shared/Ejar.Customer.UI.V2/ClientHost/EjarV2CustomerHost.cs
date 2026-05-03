using ACommerce.ClientHost;
using Ejar.Customer.UI;
using Ejar.Customer.UI.Bindings;
using Microsoft.Extensions.DependencyInjection;

namespace Ejar.Customer.UI.V2.ClientHost;

/// <summary>
/// النقطة الوحيدة التي يَستدعيها <c>Ejar.Web.V2</c> أو <c>Ejar.Maui.V2</c>:
/// <code>builder.Services.AddEjarCustomerV2();</code>
///
/// <para>الفرق الجوهريّ عن V1: لا توجد صفحات بمنطق نطاقيّ هنا. كلّ
/// صفحة في <c>Components/Pages/</c> هي wrapper بـ سطر أو سطرَين حول
/// kit widget. النَموذج البَنيويّ:</para>
/// <list type="bullet">
///   <item>الـ shim (Ejar.Web.V2) لا يَعرف عن kits — يُحمّل الـ Razor
///         components من هذه المكتبة عبر <c>AppAssembly</c>.</item>
///   <item>الـ widgets تَعمل ضدّ <c>IXxxStore</c> interfaces — التطبيق
///         يَربطها بتنفيذات إيجار <c>EjarXxxStore</c> القائمة في V1.</item>
///   <item>كلّ خدمات إيجار (AppStore، EjarChatClient، ApiReader…) تأتي
///         من <c>AddEjarCustomerUI()</c> في V1 — لا تَكرار.</item>
/// </list>
/// </summary>
public static class EjarV2CustomerHostExtensions
{
    public static IServiceCollection AddEjarCustomerV2(this IServiceCollection services)
    {
        // V1's services + bindings + AppStore + auth state provider — كلّها مُسجَّلة
        // عبر AddEjarCustomerUI(). V2 يَستهلكها كما هي.
        services.AddEjarCustomerUI();

        // ClientHost: بدون AddAppPages (الصفحات هنا في Components/Pages بـ
        // @page directives — Router القياسيّ يَكتشفها). فقط XSS guards
        // + ربط kit stores.
        services.AddACommerceClientHost(client => client
            .UseUrlAllowlist(a => a.Add(
                "cdn.ejar.sa",
                "storage.googleapis.com",
                "firebasestorage.googleapis.com"))

            .AddDomainBindings(b => b
                .Use<ACommerce.Kits.Auth.Frontend.Customer.Stores.IAuthStore,                       EjarAuthStore>()
                .Use<ACommerce.Kits.Listings.Frontend.Customer.Stores.IListingsStore,               EjarListingsStore>()
                .Use<ACommerce.Kits.Chat.Frontend.Customer.Stores.IChatStore,                       EjarChatStore>()
                .Use<ACommerce.Kits.Notifications.Frontend.Customer.Stores.INotificationsStore,     EjarNotificationsStore>()
                .Use<ACommerce.Kits.Profiles.Frontend.Customer.Stores.IProfileStore,                EjarProfileStore>()
                .Use<ACommerce.Kits.Subscriptions.Frontend.Customer.Stores.ISubscriptionsStore,     EjarSubscriptionsStore>()
                .Use<ACommerce.Kits.Support.Frontend.Customer.Stores.ISupportStore,                 EjarSupportStore>()
                .Use<ACommerce.Kits.Favorites.Frontend.Customer.Stores.IFavoritesStore,             EjarFavoritesStore>()));

        return services;
    }
}
