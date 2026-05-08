using ACommerce.ClientHost;
using ACommerce.ClientHost.Auth;
using ACommerce.ClientHost.KitApi;
using ACommerce.Kits.Auth.Frontend.Customer;
using ACommerce.Kits.Auth.Frontend.Customer.Stores;
using ACommerce.Kits.Chat.Frontend.Customer;
using ACommerce.Kits.Chat.Frontend.Customer.Stores;
using ACommerce.Kits.Favorites.Frontend.Customer;
using ACommerce.Kits.Favorites.Frontend.Customer.Stores;
using ACommerce.Kits.Listings.Frontend.Customer;
using ACommerce.Kits.Listings.Frontend.Customer.Stores;
using ACommerce.Kits.Notifications.Frontend.Customer;
using ACommerce.Kits.Notifications.Frontend.Customer.Stores;
using ACommerce.Kits.Profiles.Frontend.Customer;
using ACommerce.Kits.Profiles.Frontend.Customer.Stores;
using ACommerce.Kits.Subscriptions.Frontend.Customer;
using ACommerce.Kits.Subscriptions.Frontend.Customer.Stores;
using ACommerce.Kits.Support.Frontend.Customer;
using ACommerce.Kits.Support.Frontend.Customer.Stores;
using Ejar.Customer.UI.V2.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Ejar.Customer.UI.V2.ClientHost;

/// <summary>
/// V2 thin host — مُستقلّ تماماً عن V1. كلّ ما هو app-specific محصور هنا:
/// <list type="bullet">
///   <item>اسم الـ HttpClient ("ejar")</item>
///   <item>مَفتاح localStorage ("ejar.v2.auth")</item>
///   <item>اسم scheme المُصادَقة ("EjarV2Auth")</item>
///   <item>قائمة المُضيفين المسموح بها</item>
///   <item>خَريطة الـ routes (URL → widget)</item>
///   <item>صَفحَتا التَطبيق المخصّصتان (EjarHomeWidget، EjarDashboardWidget)</item>
/// </list>
///
/// <para>كلّ شَيء آخَر — Auth state، Persistence، AuthStateProvider،
/// AuthenticatedHttpClient، تَنفيذ كلّ <see cref="IAuthStore"/>،
/// <see cref="IListingsStore"/>… <see cref="IFavoritesStore"/> — يَأتي من
/// ClientHost والكيتس مُباشرةً (Default<i>X</i>Store).</para>
///
/// <para>التَطبيق يَستبدل أيّ Default store ببنفسه فقط حين يَحتاج سُلوكاً
/// مُختلفاً (realtime، optimistic update، sync). حالياً لا يَحتاج.</para>
/// </summary>
public static class EjarV2CustomerHostExtensions
{
    public static IServiceCollection AddEjarCustomerV2(this IServiceCollection services)
    {
        // ─── Auth machinery (state + persistence + provider + http client) ──
        services.AddClientAuth(o =>
        {
            o.HttpClientName = "ejar";
            o.StorageKey     = "ejar.v2.auth";
            o.Scheme         = "EjarV2Auth";
        });

        // ─── KitApi pipeline + kit api clients ────────────────────────────
        services.AddKitApiPipeline(sp => sp.GetRequiredService<AuthenticatedHttpClient>().Client);
        services.AddScoped<IAuthApiClient,          HttpAuthApiClient>();
        services.AddScoped<IListingsApiClient,      HttpListingsApiClient>();
        services.AddScoped<IChatApiClient,          HttpChatApiClient>();
        services.AddScoped<INotificationsApiClient, HttpNotificationsApiClient>();
        services.AddScoped<IProfileApiClient,       HttpProfileApiClient>();
        services.AddScoped<ISubscriptionsApiClient, HttpSubscriptionsApiClient>();
        services.AddScoped<ISupportApiClient,       HttpSupportApiClient>();
        services.AddScoped<IFavoritesApiClient,     HttpFavoritesApiClient>();

        // ─── Routes + Layout + Default Bindings ──────────────────────────
        services.AddACommerceClientHost(client => client
            .UseUrlAllowlist(a => a.Add(
                "cdn.ejar.sa",
                "storage.googleapis.com",
                "firebasestorage.googleapis.com"))
            .AddAppPages(p => p
                .Add("/",                 typeof(EjarHomeWidget))
                .Add("/dashboard",        typeof(EjarDashboardWidget), requiresAuth: true)
                .Add("/login",            AuthWidgets.Login)
                .Add("/listings",         ListingsWidgets.Explore)
                .Add("/listings/{id}",    ListingsWidgets.Details)
                .Add("/properties",       ListingsWidgets.Explore)
                .Add("/properties/{id}",  ListingsWidgets.Details)
                .Add("/my-listings",      ListingsWidgets.Mine,     requiresAuth: true)
                .Add("/my-listings/new",  ListingsWidgets.Create,   requiresAuth: true)
                .Add("/chat",             ChatWidgets.Inbox,        requiresAuth: true)
                .Add("/chat/{id}",        ChatWidgets.Room,         requiresAuth: true)
                .Add("/notifications",    NotificationsWidgets.Inbox,requiresAuth: true)
                .Add("/me",               ProfilesWidgets.Profile,  requiresAuth: true)
                .Add("/plans",            SubscriptionsWidgets.Plans)
                .Add("/support",          SupportWidgets.Tickets,   requiresAuth: true)
                .Add("/favorites",        FavoritesWidgets.List,    requiresAuth: true))
            .AddDomainBindings(b => b
                .Use<IAuthStore,          DefaultAuthStore>()
                .Use<IListingsStore,      DefaultListingsStore>()
                .Use<IChatStore,          DefaultChatStore>()
                .Use<INotificationsStore, DefaultNotificationsStore>()
                .Use<IProfileStore,       DefaultProfileStore>()
                .Use<ISubscriptionsStore, DefaultSubscriptionsStore>()
                .Use<ISupportStore,       DefaultSupportStore>()
                .Use<IFavoritesStore,     DefaultFavoritesStore>()));

        return services;
    }
}
