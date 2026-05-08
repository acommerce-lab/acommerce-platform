using ACommerce.ClientHost;
using ACommerce.ClientHost.KitApi;
using ACommerce.Kits.Auth.Frontend.Customer;
using ACommerce.Kits.Chat.Frontend.Customer;
using ACommerce.Kits.Favorites.Frontend.Customer;
using ACommerce.Kits.Listings.Frontend.Customer;
using ACommerce.Kits.Notifications.Frontend.Customer;
using ACommerce.Kits.Profiles.Frontend.Customer;
using ACommerce.Kits.Subscriptions.Frontend.Customer;
using ACommerce.Kits.Support.Frontend.Customer;
using Ejar.Customer.UI.V2.Bindings;
using Ejar.Customer.UI.V2.Components.Pages;
using Ejar.Customer.UI.V2.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Ejar.Customer.UI.V2.ClientHost;

/// <summary>
/// V2 thin host — مُستقلّ تماماً عن V1. يَحوي فقط:
/// <list type="bullet">
///   <item>AppStore + Persistence + HttpClient (٣ خَدَمات)</item>
///   <item>AuthenticationStateProvider</item>
///   <item>KitApi pipeline (HttpClient → كل kit api clients)</item>
///   <item>٨ Bindings (واحدة لكل kit، تَدلّع لـ api client)</item>
///   <item>تَسجيل routes الكيتس</item>
/// </list>
///
/// <para>الخَدَمات العَرَضيّة (FavoritesSync، UnreadService، FirebasePush،
/// Realtime، VersionPoll) <b>ليست</b> هنا — مَنطقها cross-cutting يَجب أن
/// يَكون داخل الكيت أو composition، لا في كل تطبيق.</para>
/// </summary>
public static class EjarV2CustomerHostExtensions
{
    public static IServiceCollection AddEjarCustomerV2(this IServiceCollection services)
    {
        // ─── الحالة + الاستعادة + HTTP ────────────────────────────────
        services.AddScoped<EjarV2AppStore>();
        services.AddScoped<EjarV2Persistence>();
        services.AddScoped<EjarV2HttpClient>();

        // ─── Authentication ────────────────────────────────────────────
        services.AddAuthorizationCore();
        services.AddScoped<AuthenticationStateProvider, EjarV2AuthStateProvider>();

        // ─── KitApi pipeline + kit api clients ────────────────────────
        services.AddKitApiPipeline(sp => sp.GetRequiredService<EjarV2HttpClient>().Client);
        services.AddScoped<ACommerce.Kits.Auth.Frontend.Customer.Stores.IAuthApiClient,
                          ACommerce.Kits.Auth.Frontend.Customer.Stores.HttpAuthApiClient>();
        services.AddScoped<ACommerce.Kits.Listings.Frontend.Customer.Stores.IListingsApiClient,
                          ACommerce.Kits.Listings.Frontend.Customer.Stores.HttpListingsApiClient>();
        services.AddScoped<ACommerce.Kits.Chat.Frontend.Customer.Stores.IChatApiClient,
                          ACommerce.Kits.Chat.Frontend.Customer.Stores.HttpChatApiClient>();
        services.AddScoped<ACommerce.Kits.Notifications.Frontend.Customer.Stores.INotificationsApiClient,
                          ACommerce.Kits.Notifications.Frontend.Customer.Stores.HttpNotificationsApiClient>();
        services.AddScoped<ACommerce.Kits.Profiles.Frontend.Customer.Stores.IProfileApiClient,
                          ACommerce.Kits.Profiles.Frontend.Customer.Stores.HttpProfileApiClient>();
        services.AddScoped<ACommerce.Kits.Subscriptions.Frontend.Customer.Stores.ISubscriptionsApiClient,
                          ACommerce.Kits.Subscriptions.Frontend.Customer.Stores.HttpSubscriptionsApiClient>();
        services.AddScoped<ACommerce.Kits.Support.Frontend.Customer.Stores.ISupportApiClient,
                          ACommerce.Kits.Support.Frontend.Customer.Stores.HttpSupportApiClient>();
        services.AddScoped<ACommerce.Kits.Favorites.Frontend.Customer.Stores.IFavoritesApiClient,
                          ACommerce.Kits.Favorites.Frontend.Customer.Stores.HttpFavoritesApiClient>();

        // ─── Routes + Layout + Bindings ────────────────────────────────
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
                .Use<ACommerce.Kits.Auth.Frontend.Customer.Stores.IAuthStore,                       EjarV2AuthStore>()
                .Use<ACommerce.Kits.Listings.Frontend.Customer.Stores.IListingsStore,               EjarV2ListingsStore>()
                .Use<ACommerce.Kits.Chat.Frontend.Customer.Stores.IChatStore,                       EjarV2ChatStore>()
                .Use<ACommerce.Kits.Notifications.Frontend.Customer.Stores.INotificationsStore,     EjarV2NotificationsStore>()
                .Use<ACommerce.Kits.Profiles.Frontend.Customer.Stores.IProfileStore,                EjarV2ProfileStore>()
                .Use<ACommerce.Kits.Subscriptions.Frontend.Customer.Stores.ISubscriptionsStore,     EjarV2SubscriptionsStore>()
                .Use<ACommerce.Kits.Support.Frontend.Customer.Stores.ISupportStore,                 EjarV2SupportStore>()
                .Use<ACommerce.Kits.Favorites.Frontend.Customer.Stores.IFavoritesStore,             EjarV2FavoritesStore>()));

        return services;
    }
}
