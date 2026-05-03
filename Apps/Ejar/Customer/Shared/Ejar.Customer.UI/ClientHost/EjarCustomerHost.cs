using ACommerce.ClientHost;
using ACommerce.Kits.Auth.Frontend.Customer;
using ACommerce.Kits.Chat.Frontend.Customer;
using ACommerce.Kits.Favorites.Frontend.Customer;
using ACommerce.Kits.Listings.Frontend.Customer;
using ACommerce.Kits.Notifications.Frontend.Customer;
using ACommerce.Kits.Profiles.Frontend.Customer;
using ACommerce.Kits.Subscriptions.Frontend.Customer;
using ACommerce.Kits.Support.Frontend.Customer;
using Ejar.Customer.UI.Bindings;
using Microsoft.Extensions.DependencyInjection;

namespace Ejar.Customer.UI.ClientHost;

/// <summary>
/// النقطة الوحيدة التي يَستدعيها <c>Ejar.Web</c> أو <c>Ejar.Maui</c>:
/// <code>builder.Services.AddEjarCustomer();</code>
///
/// <para>
/// النَموذج: الكيتس تَكشف <c>XxxWidgets</c> (مكوّنات Razor بدون
/// <c>@page</c>)، التطبيق هو الذي يَختار route + layout. هذا يَسمح
/// بإعادة تركيب نفس الـ widgets بأشكال مختلفة في تطبيقات مختلفة.
/// </para>
/// </summary>
public static class EjarCustomerHostExtensions
{
    public static IServiceCollection AddEjarCustomer(this IServiceCollection services) =>
        services.AddACommerceClientHost(client => client

            // ── XSS guards ───────────────────────────────────────────
            .UseUrlAllowlist(a => a.Add(
                "cdn.ejar.sa",
                "storage.googleapis.com",
                "firebasestorage.googleapis.com"))

            // ── ربط widgets الكيتس بـ routes إيجار ────────────────────
            // الكيتس تَكشف Types فقط — التطبيق يَختار المسارات + يُعيد
            // التسمية إن لزم. ايجار يَستعمل /properties بدل /listings.
            .AddAppPages(p => p
                // Auth
                .Add("/login",            AuthWidgets.Login)

                // Listings (renamed routes — وكأنّ "Rename" قديم)
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
                .Add("/favorites",        FavoritesWidgets.List,         requiresAuth: true)

                // app-only composition (يُجمّع widgets من ≥2 kit)
                .Add("/dashboard",        typeof(Pages.EjarDashboardPage), requiresAuth: true))

            // ── ربط الـ stores بتنفيذات إيجار ────────────────────────
            // كلّ store يَستهلك خِدمات إيجار الموجودة لكنّه يَعرضها عبر
            // interface kit-aware. الكيت لا يَعرف عن EjarChatStore — يَرى
            // IChatStore فقط.
            .AddDomainBindings(b => b
                .Use<ACommerce.Kits.Auth.Frontend.Customer.Stores.IAuthStore,                       EjarAuthStore>()
                .Use<ACommerce.Kits.Listings.Frontend.Customer.Stores.IListingsStore,               EjarListingsStore>()
                .Use<ACommerce.Kits.Chat.Frontend.Customer.Stores.IChatStore,                       EjarChatStore>()
                .Use<ACommerce.Kits.Notifications.Frontend.Customer.Stores.INotificationsStore,     EjarNotificationsStore>()
                .Use<ACommerce.Kits.Profiles.Frontend.Customer.Stores.IProfileStore,                EjarProfileStore>()
                .Use<ACommerce.Kits.Subscriptions.Frontend.Customer.Stores.ISubscriptionsStore,     EjarSubscriptionsStore>()
                .Use<ACommerce.Kits.Support.Frontend.Customer.Stores.ISupportStore,                 EjarSupportStore>()
                .Use<ACommerce.Kits.Favorites.Frontend.Customer.Stores.IFavoritesStore,             EjarFavoritesStore>())
        );
}
