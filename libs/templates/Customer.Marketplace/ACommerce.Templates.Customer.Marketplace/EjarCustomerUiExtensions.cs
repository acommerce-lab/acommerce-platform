using ACommerce.Chat.Client.Blazor;
using ACommerce.Client.Http;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.ClientHost.Auth;
using ACommerce.ClientHost.KitApi;
using ACommerce.ClientHost.Operations;
using ACommerce.Compositions.Customer.Chat.Realtime;
using ACommerce.Compositions.Customer.Favorites.Realtime;
using ACommerce.Compositions.Customer.Notifications.Realtime;
using ACommerce.Compositions.Customer.Unread;
using ACommerce.Culture.Abstractions;
using ACommerce.Culture.Defaults;
using ACommerce.Culture.Interceptors;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.Kits.Auth.Frontend.Customer.Stores;
using ACommerce.Kits.Chat.Frontend.Customer.Stores;
using ACommerce.Kits.Favorites.Frontend.Customer.Stores;
using ACommerce.Kits.Listings.Frontend.Customer.Stores;
using ACommerce.Kits.Notifications.Frontend.Customer.Stores;
using ACommerce.Kits.Profiles.Frontend.Customer.Stores;
using ACommerce.Kits.Subscriptions.Frontend.Customer.Stores;
using ACommerce.Kits.Support.Frontend.Customer.Stores;
using ACommerce.Kits.Versions.Templates;
using ACommerce.L10n.Blazor;
using ACommerce.OperationEngine.Core;
using ACommerce.Subscriptions.Templates.Extensions;
using ACommerce.Templates.Shared.Models;
using Ejar.Customer.UI.Services;
using Ejar.Customer.UI.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ejar.Customer.UI;

/// <summary>
/// تَسجيل DI الموحَّد لِكلّ خَدَمات قالَب Customer.Marketplace.
///
/// <para>F57: Auth → ClientHost.Auth.</para>
/// <para>F58: VersionPoll → Versions.Templates kit.</para>
/// <para>F59: Culture handlers + interceptors + L10n → kits:
/// <list type="bullet">
///   <item><c>CultureHeadersHandler</c> ← Culture.Defaults (يَستَهلِك ICultureContext)</item>
///   <item><c>CultureInterceptor</c> ← Culture.Interceptors (يَستَهلِك ICultureContext)</item>
///   <item><c>L</c> + <c>ITranslationProvider</c> + <c>ILanguageContext</c> ← L10n.Blazor</item>
///   <item><c>AppStoreCultureContext</c> adapter يَكشِف AppStore.Ui كَ ICultureContext + ILanguageContext</item>
/// </list></para>
/// </summary>
public static class EjarCustomerUiExtensions
{
    public static IServiceCollection AddEjarCustomerUI(this IServiceCollection services)
    {
        // ─── Auth machinery (ClientHost.Auth) ──────────────────────────
        services.AddClientAuth(o =>
        {
            o.HttpClientName = "ejar";
            o.StorageKey     = "ejar.auth";
            o.Scheme         = "EjarAuth";
        });

        // ─── Store ─────────────────────────────────────────────────────
        services.AddScoped<AppStore>();
        services.AddScoped<AppStorePersistence>();
        services.AddScoped<ITemplateStore>(sp => sp.GetRequiredService<AppStore>());

        // ─── Culture: AppStore adapter يَخدِم ICultureContext + ILanguageContext ─
        services.AddScoped<AppStoreCultureContext>();
        services.AddScoped<ICultureContext>(sp => sp.GetRequiredService<AppStoreCultureContext>());
        services.AddScoped<ILanguageContext>(sp => sp.GetRequiredService<AppStoreCultureContext>());

        // ─── L10n: L10n.Blazor's L + Ejar's resx-backed provider ────────
        services.AddScoped<ITranslationProvider, EjarTranslationProvider>();
        services.AddScoped<L>();

        // ─── Culture utilities (timezone / numerals) ───────────────────
        services.AddScoped<ITimezoneProvider, JsTimezoneProvider>();
        services.AddSingleton<ACommerce.Culture.Abstractions.INumeralNormalizer, DefaultNumeralNormalizer>();

        // ─── HTTP handlers مِن الكيتس ─────────────────────────────────
        services.AddTransient<CultureHeadersHandler>();
        services.AddScoped<CultureInterceptor>();

        // ─── Versions Kit (Templates + Poll + Headers handler) ─────────
        services.AddVersionsTemplates(httpClientName: "ejar");

        // ─── Subscriptions Kit (frontend) ─────────────────────────────
        services.AddSubscriptionsTemplates();

        // ─── OAM client engine + 8 kit routes ──────────────────────────
        // ClientHost.Operations يَحقن: OpEngine + HttpRouteRegistry (factory)
        // + HttpDispatcher + ClientOpEngine + ITemplateEngine. كلّ kit
        // يُسَجِّل IRoutesRegistrar فتُجمَع في الـ registry تلقائيّاً.
        services.AddClientOpEngine();
        services.AddAuthRoutes();
        services.AddListingsRoutes();
        services.AddChatRoutes();
        services.AddNotificationsRoutes();
        services.AddProfilesRoutes();
        services.AddSubscriptionsRoutes();
        services.AddSupportRoutes();
        services.AddFavoritesRoutes();

        // ApiReader (V1 legacy) — pages تَستَهلِكه مُباشَرة لِنِداءات
        // غير-OAM (cities، amenities، complaints…).
        services.AddScoped<ApiReader>(sp =>
        {
            var http = sp.GetRequiredService<AuthenticatedHttpClient>();
            return new ApiReader(http.Client, sp.GetRequiredService<CultureInterceptor>());
        });

        services.AddScoped<FavoritesSync>();
        services.AddScoped<FirebasePushService>();

        // ─── KitApi pipeline ────────────────────────────────────────────
        services.AddKitApiPipeline(sp =>
            sp.GetRequiredService<AuthenticatedHttpClient>().Client);

        // ─── kit ApiClients ────────────────────────────────────────────
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

        // ─── kit IXxxStore bindings (Default<Kit>Stores OAM-driven) ────
        // F63: تَطبيق إيجار V1 يُسَجِّل الكيتس Stores رَسميّاً ⇒
        // compositions تَستَقبِلها (UnreadComposition، Realtime ingestors).
        services.AddScoped<ACommerce.Kits.Auth.Frontend.Customer.Stores.IAuthStore,
                          ACommerce.Kits.Auth.Frontend.Customer.Stores.DefaultAuthStore>();
        services.AddScoped<ACommerce.Kits.Listings.Frontend.Customer.Stores.IListingsStore,
                          ACommerce.Kits.Listings.Frontend.Customer.Stores.DefaultListingsStore>();
        services.AddScoped<ACommerce.Kits.Chat.Frontend.Customer.Stores.IChatStore,
                          ACommerce.Kits.Chat.Frontend.Customer.Stores.DefaultChatStore>();
        services.AddScoped<ACommerce.Kits.Notifications.Frontend.Customer.Stores.INotificationsStore,
                          ACommerce.Kits.Notifications.Frontend.Customer.Stores.DefaultNotificationsStore>();
        services.AddScoped<ACommerce.Kits.Profiles.Frontend.Customer.Stores.IProfileStore,
                          ACommerce.Kits.Profiles.Frontend.Customer.Stores.DefaultProfileStore>();
        services.AddScoped<ACommerce.Kits.Subscriptions.Frontend.Customer.Stores.ISubscriptionsStore,
                          ACommerce.Kits.Subscriptions.Frontend.Customer.Stores.DefaultSubscriptionsStore>();
        services.AddScoped<ACommerce.Kits.Support.Frontend.Customer.Stores.ISupportStore,
                          ACommerce.Kits.Support.Frontend.Customer.Stores.DefaultSupportStore>();
        services.AddScoped<ACommerce.Kits.Favorites.Frontend.Customer.Stores.IFavoritesStore,
                          ACommerce.Kits.Favorites.Frontend.Customer.Stores.DefaultFavoritesStore>();

        // ─── State bridge: empty interpreter registry + AppStateApplier ─
        // Pages still inject AppStateApplier (will be removed in a future
        // page-by-page cleanup). The registry is empty; ApplyAsync is no-op.
        services.AddScoped<OperationInterpreterRegistry<AppStore>>(sp =>
            new OperationInterpreterRegistry<AppStore>(
                sp.GetRequiredService<ILogger<OperationInterpreterRegistry<AppStore>>>()));
        services.AddScoped<AppStateApplier>();
        services.AddScoped<IStateApplier>(sp => sp.GetRequiredService<AppStateApplier>());

        // ─── Realtime + Chat client ────────────────────────────────────
        services.AddScoped<EjarRealtimeService>();
        services.AddScoped<UnreadService>();
        services.AddScoped<IChatClient, EjarChatClient>();

        // ─── Compositions (cross-kit + realtime ingestors) ─────────────
        // V1's سيرفَر يَبُثّ عَبر SignalR Hub ⇒ لا حاجَة broadcaster client-side،
        // فَقَط ingestors تَستَقبِل الأَحداث الواردة وتَدفَعها لِكيت Stores.
        services.AddCustomerUnreadComposition();
        services.AddChatRealtimeIngestor();
        services.AddNotificationsRealtimeComposition();
        services.AddFavoritesRealtimeComposition();

        // ─── OAM-seam cross-cutting interceptors ───────────────────────
        // CultureLocalizationInterceptor يَعمَل عَلى كلّ http.send post-phase
        // ⇒ تَعريب التَواريخ + العُملات في المَغلَّفات تلقائيّاً (ApiReader
        // كانَت تَستَدعيه يَدويّاً في كلّ GET — الآن مُسَجَّل مرّة واحدة).
        services.AddScoped<IOperationInterceptor, CultureLocalizationInterceptor>();

        return services;
    }
}
