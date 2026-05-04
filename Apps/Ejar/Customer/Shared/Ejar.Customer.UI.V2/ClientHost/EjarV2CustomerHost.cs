using ACommerce.ClientHost;
using ACommerce.Kits.Auth.Frontend.Customer;
using ACommerce.Kits.Chat.Frontend.Customer;
using ACommerce.Kits.Favorites.Frontend.Customer;
using ACommerce.Kits.Listings.Frontend.Customer;
using ACommerce.Kits.Notifications.Frontend.Customer;
using ACommerce.Kits.Profiles.Frontend.Customer;
using ACommerce.Kits.Subscriptions.Frontend.Customer;
using ACommerce.Kits.Support.Frontend.Customer;
using Ejar.Customer.UI;
using Ejar.Customer.UI.Bindings;
using Ejar.Customer.UI.V2.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Ejar.Customer.UI.V2.ClientHost;

/// <summary>
/// النقطة الوحيدة التي يَستدعيها <c>Ejar.Web.V2</c>:
/// <code>builder.Services.AddEjarCustomerV2();</code>
///
/// <para>الفرق الجوهريّ عن V1: لا توجد @page wrappers أصلاً. كلّ المسارات
/// في <see cref="AppPageBuilder"/> هنا — مَكان واحد، type-safe، قابل
/// للـ override برمجيّاً. الـ <c>HostedApp</c> يَلفّ kit widgets بـ
/// <c>MainLayout</c> ويَفرض <c>RequiresAuth</c>.</para>
/// </summary>
public static class EjarV2CustomerHostExtensions
{
    public static IServiceCollection AddEjarCustomerV2(this IServiceCollection services)
    {
        // V1 services (AppStore، EjarChatClient، ApiReader، …) — كلّها تَأتي
        // من AddEjarCustomerUI() دون تَكرار.
        services.AddEjarCustomerUI();

        services.AddACommerceClientHost(client => client
            .UseUrlAllowlist(a => a.Add(
                "cdn.ejar.sa",
                "storage.googleapis.com",
                "firebasestorage.googleapis.com"))

            // ── كلّ مسارات إيجار V2 — type-safe، مكان واحد ──────────
            .AddAppPages(p => p
                // app-only composition pages (تَدمج widgets من ≥1 kit)
                .Add("/",                 typeof(EjarHomeWidget))
                .Add("/dashboard",        typeof(EjarDashboardWidget), requiresAuth: true)

                // Auth
                .Add("/login",            AuthWidgets.Login)

                // Listings — يَربط الـ widget إلى /listings/{id} hardcoded،
                // فنُسَجِّل المسار حرفيّاً. /properties alias للوصول من الـ
                // brand text لكنّ الروابط الفعليّة تَستعمل /listings.
                .Add("/listings",         ListingsWidgets.Explore)
                .Add("/listings/{id}",    ListingsWidgets.Details)
                .Add("/properties",       ListingsWidgets.Explore)
                .Add("/properties/{id}",  ListingsWidgets.Details)
                .Add("/my-listings",      ListingsWidgets.Mine,    requiresAuth: true)
                .Add("/my-listings/new",  ListingsWidgets.Create,  requiresAuth: true)

                // Chat
                .Add("/chat",             ChatWidgets.Inbox,             requiresAuth: true)
                .Add("/chat/{id}",        ChatWidgets.Room,              requiresAuth: true)

                // Notifications
                .Add("/notifications",    NotificationsWidgets.Inbox,    requiresAuth: true)

                // Profile
                .Add("/me",               ProfilesWidgets.Profile,       requiresAuth: true)

                // Subscriptions
                .Add("/plans",            SubscriptionsWidgets.Plans)

                // Support
                .Add("/support",          SupportWidgets.Tickets,        requiresAuth: true)

                // Favorites
                .Add("/favorites",        FavoritesWidgets.List,         requiresAuth: true))

            // ── ربط kit stores بتنفيذات إيجار ────────────────────────
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
