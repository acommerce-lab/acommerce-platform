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
/// <code>builder.Services.AddEjarCustomer(builder.Configuration);</code>
/// كلّ شيء — صفحات الكيتس، الـ stores، الـ branding، الـ navigation،
/// الـ compositions — يُسجَّل من هنا. الـ shims لا تَعرف عن الكيتس.
/// </summary>
public static class EjarCustomerHostExtensions
{
    public static IServiceCollection AddEjarCustomer(this IServiceCollection services) =>
        services.AddACommerceClientHost(client => client

            // ── XSS guards ───────────────────────────────────────────
            // الـ default sanitizer يُجرّد كلّ HTML. لو احتجنا markdown مستقبلاً
            // نُسجِّل MarkdownSanitizer بدلاً منه.
            .UseUrlAllowlist(a => a.Add(
                "cdn.ejar.sa",
                "storage.googleapis.com",
                "firebasestorage.googleapis.com"))

            // ── kit pages ────────────────────────────────────────────
            // التطبيق يَختار من بَين صفحات الكيتس + يُعيد التسمية حسب الحاجة.
            // ايجار يَستعمل "/properties" بدل "/listings" في الـ UX.
            .AddKitPages(p => p
                .Add<AuthPageBundle>()
                .Add<ListingsPageBundle>(o => o
                    .Rename("listings.index",  "/properties")
                    .Rename("listings.detail", "/properties/{id}"))
                .Add<ChatPageBundle>()
                .Add<NotificationsPageBundle>()
                .Add<ProfilesPageBundle>()
                .Add<SubscriptionsPageBundle>()
                .Add<SupportPageBundle>()
                .Add<FavoritesPageBundle>())

            // ── ربط الـ stores بتنفيذات إيجار ────────────────────────
            // كلّ store يَستهلك خِدمات إيجار الموجودة (HTTP، realtime، AppStore)
            // لكنّه يَعرضها عبر interface kit-aware. الكيت لا يَعرف بأنّ
            // التنفيذ هو Ejar — يَرى IListingsStore فقط.
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
