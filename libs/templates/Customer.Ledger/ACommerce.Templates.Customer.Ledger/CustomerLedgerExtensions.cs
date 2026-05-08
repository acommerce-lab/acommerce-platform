using ACommerce.ClientHost;
using ACommerce.ClientHost.Auth;
using ACommerce.ClientHost.KitApi;
using ACommerce.ClientHost.Pages;
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
using Microsoft.Extensions.DependencyInjection;

namespace ACommerce.Templates.Customer.Ledger;

/// <summary>
/// نُقطة دَخول قالَب Customer Ledger. استدعاء واحِد يُسَجِّل:
/// <list type="bullet">
///   <item>AddClientAuth(HttpClient, StorageKey, Scheme) — مَع option للإيقاف.</item>
///   <item>KitApi pipeline + ٨ kit api clients (Auth/Listings/Chat/Notifications/Profiles/Subscriptions/Support/Favorites).</item>
///   <item>٨ Default<i>X</i>Store bindings (override-able عَبر <c>StoreOverrides</c>).</item>
///   <item>routes الافتراضيّة لـ ١٦ صَفحة (login, listings*, chat*, notifications, me, plans, support, favorites…).</item>
///   <item>UrlAllowlist + ExtraPages تَطبيقيّة.</item>
/// </list>
///
/// <para>التَطبيقات لا تَحتاج كتابة أيّ Binding أو Route تَكراريّ. لو احتاج
/// تَطبيق سُلوكاً مُختَلِفاً (Realtime chat، Optimistic favorites)، يُمَرِّر
/// النَوع البَديل في <see cref="CustomerLedgerOptions.StoreOverrides"/> ولا
/// يَحتاج لَمس باقي القالَب.</para>
/// </summary>
public static class CustomerLedgerExtensions
{
    public static IServiceCollection AddTemplate_Customer_Ledger(
        this IServiceCollection services,
        Action<CustomerLedgerOptions> configure)
    {
        var opts = new CustomerLedgerOptions();
        configure(opts);

        // ─── Auth machinery ──────────────────────────────────────────────
        if (opts.RegisterAuth)
        {
            services.AddClientAuth(o =>
            {
                o.HttpClientName = opts.HttpClientName;
                o.StorageKey     = opts.StorageKey;
                o.Scheme         = opts.Scheme;
            });
        }

        // ─── KitApi pipeline + kit api clients ───────────────────────────
        services.AddKitApiPipeline(sp => sp.GetRequiredService<AuthenticatedHttpClient>().Client);
        services.AddScoped<IAuthApiClient,          HttpAuthApiClient>();
        services.AddScoped<IListingsApiClient,      HttpListingsApiClient>();
        services.AddScoped<IChatApiClient,          HttpChatApiClient>();
        services.AddScoped<INotificationsApiClient, HttpNotificationsApiClient>();
        services.AddScoped<IProfileApiClient,       HttpProfileApiClient>();
        services.AddScoped<ISubscriptionsApiClient, HttpSubscriptionsApiClient>();
        services.AddScoped<ISupportApiClient,       HttpSupportApiClient>();
        services.AddScoped<IFavoritesApiClient,     HttpFavoritesApiClient>();

        // ─── routes + bindings + allowlist ───────────────────────────────
        services.AddACommerceClientHost(client =>
        {
            client.UseUrlAllowlist(a =>
            {
                foreach (var host in opts.UrlAllowlist) a.Add(host);
            });

            client.AddAppPages(p =>
            {
                AddIfIncluded(p, opts, "login",            "/login",            AuthWidgets.Login);
                AddIfIncluded(p, opts, "listings",         "/listings",         ListingsWidgets.Explore);
                AddIfIncluded(p, opts, "listing-details",  "/listings/{id}",    ListingsWidgets.Details);
                AddIfIncluded(p, opts, "properties",       "/properties",       ListingsWidgets.Explore);
                AddIfIncluded(p, opts, "property-details", "/properties/{id}",  ListingsWidgets.Details);
                AddIfIncluded(p, opts, "my-listings",      "/my-listings",      ListingsWidgets.Mine,    requiresAuth: true);
                AddIfIncluded(p, opts, "my-listings-new",  "/my-listings/new",  ListingsWidgets.Create,  requiresAuth: true);
                AddIfIncluded(p, opts, "chat",             "/chat",             ChatWidgets.Inbox,       requiresAuth: true);
                AddIfIncluded(p, opts, "chat-room",        "/chat/{id}",        ChatWidgets.Room,        requiresAuth: true);
                AddIfIncluded(p, opts, "notifications",    "/notifications",    NotificationsWidgets.Inbox, requiresAuth: true);
                AddIfIncluded(p, opts, "me",               "/me",               ProfilesWidgets.Profile, requiresAuth: true);
                AddIfIncluded(p, opts, "plans",            "/plans",            SubscriptionsWidgets.Plans);
                AddIfIncluded(p, opts, "support",          "/support",          SupportWidgets.Tickets,  requiresAuth: true);
                AddIfIncluded(p, opts, "favorites",        "/favorites",        FavoritesWidgets.List,   requiresAuth: true);

                foreach (var (route, component, requiresAuth) in opts.ExtraPages)
                    p.Add(route, component, requiresAuth);
            });

            client.AddDomainBindings(b =>
            {
                Bind<IAuthStore,          DefaultAuthStore>(b, opts, "auth");
                Bind<IListingsStore,      DefaultListingsStore>(b, opts, "listings");
                Bind<IChatStore,          DefaultChatStore>(b, opts, "chat");
                Bind<INotificationsStore, DefaultNotificationsStore>(b, opts, "notifications");
                Bind<IProfileStore,       DefaultProfileStore>(b, opts, "profile");
                Bind<ISubscriptionsStore, DefaultSubscriptionsStore>(b, opts, "subscriptions");
                Bind<ISupportStore,       DefaultSupportStore>(b, opts, "support");
                Bind<IFavoritesStore,     DefaultFavoritesStore>(b, opts, "favorites");
            });
        });

        return services;
    }

    private static void AddIfIncluded(
        AppPageBuilder p, CustomerLedgerOptions opts,
        string id, string route, Type component, bool requiresAuth = false)
    {
        if (opts.ExcludedRoutes.Contains(id)) return;
        p.Add(route, component, requiresAuth);
    }

    /// <summary>
    /// يَربط interface لِكيت بِـ Default impl. لو التَطبيق مَرَّر override
    /// لِنَفس المَفتاح في <see cref="CustomerLedgerOptions.StoreOverrides"/>،
    /// نَستَعمِل البَديل بدلاً مِن Default.
    /// </summary>
    private static void Bind<TInterface, TDefault>(
        DomainBindingsBuilder b, CustomerLedgerOptions opts, string key)
        where TInterface : class
        where TDefault : class, TInterface
    {
        if (opts.StoreOverrides.TryGetValue(key, out var overrideType))
            b.Services.AddScoped(typeof(TInterface), overrideType);
        else
            b.Use<TInterface, TDefault>();
    }
}
