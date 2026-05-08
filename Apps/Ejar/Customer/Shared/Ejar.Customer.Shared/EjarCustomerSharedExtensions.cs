using ACommerce.Chat.Client.Blazor;
using ACommerce.Client.Http;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.ClientHost.KitApi;
using ACommerce.Culture.Abstractions;
using ACommerce.Culture.Defaults;
using ACommerce.Kits.Versions.Templates;
using ACommerce.OperationEngine.Core;
using ACommerce.Subscriptions.Templates.Extensions;
using ACommerce.Templates.Shared.Models;
using Ejar.Customer.UI.Interceptors;
using Ejar.Customer.UI.Interpreters;
using Ejar.Customer.UI.Operations;
using Ejar.Customer.UI.Services;
using Ejar.Customer.UI.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.Authorization;

namespace Ejar.Customer.Shared;

/// <summary>
/// تَسجيل DI الموحَّد لكلّ الخَدَمات المُشتَرَكة بين V1 و V2: AppStore،
/// Persistence، Auth state، HTTP interceptors، KitApi pipeline، kit
/// api clients، bindings، realtime، unread counters. لا يَفترض أيّ
/// shell مُعَيَّن — V1 (Ejar.Customer.UI) و V2 (Ejar.Customer.UI.V2) كلاهما
/// يَستدعيها فيَتقاسمان نَفس البنية التَحتيّة.
///
/// <para><b>المتطلَّب المُسبَق</b>: المُضيف يُسَجِّل HttpClient باسم "ejar"
/// مع BaseAddress للبيئة الحاليّة + AuthHeadersHandler/CultureHeadersHandler/
/// AppVersionHeadersHandler.</para>
/// </summary>
public static class EjarCustomerSharedExtensions
{
    public static IServiceCollection AddEjarCustomerShared(this IServiceCollection services)
    {
        // ─── Store + Translations ──────────────────────────────────────
        services.AddScoped<AppStore>();
        services.AddScoped<AppStorePersistence>();
        services.AddScoped<ITemplateStore>(sp => sp.GetRequiredService<AppStore>());

        // ─── Authentication ────────────────────────────────────────────
        services.AddAuthorizationCore();
        services.AddScoped<AuthenticationStateProvider, EjarAuthenticationStateProvider>();

        services.AddSingleton<ITranslationProvider, EmbeddedTranslationProvider>();
        services.AddScoped<L>();

        services.AddScoped<ITimezoneProvider, JsTimezoneProvider>();
        services.AddSingleton<INumeralNormalizer, DefaultNumeralNormalizer>();

        // ─── HTTP interceptors ─────────────────────────────────────────
        services.AddScoped<CultureInterceptor>();
        services.AddTransient<CultureHeadersHandler>();
        services.AddTransient<AuthHeadersHandler>();
        services.AddScoped<EjarCircuitHttp>();

        // ─── Versions Kit (frontend) ───────────────────────────────────
        services.AddVersionsTemplates(httpClientName: "ejar");

        // ─── Subscriptions Kit (frontend) ─────────────────────────────
        services.AddSubscriptionsTemplates();

        // ─── OpEngine + Routes registry + dispatcher + reader ────────
        services.AddScoped<OpEngine>(sp =>
            new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

        var routeRegistry = new HttpRouteRegistry();
        EjarRoutes.Register(routeRegistry);
        services.AddSingleton(routeRegistry);

        services.AddScoped<HttpDispatcher>(sp =>
        {
            var circuit = sp.GetRequiredService<EjarCircuitHttp>();
            return new HttpDispatcher(
                circuit.Client,
                sp.GetRequiredService<HttpRouteRegistry>(),
                sp.GetRequiredService<OpEngine>(),
                sp.GetRequiredService<ILogger<HttpDispatcher>>());
        });
        services.AddScoped<IOperationDispatcher>(sp => sp.GetRequiredService<HttpDispatcher>());

        services.AddScoped<ApiReader>(sp =>
        {
            var circuit = sp.GetRequiredService<EjarCircuitHttp>();
            return new ApiReader(circuit.Client, sp.GetRequiredService<CultureInterceptor>());
        });

        services.AddScoped<FavoritesSync>();
        services.AddScoped<FirebasePushService>();

        // ─── KitApi pipeline موحَّد ────────────────────────────────────
        services
            .AddKitApiPipeline(sp => sp.GetRequiredService<EjarCircuitHttp>().Client)
            .AddInterceptor<TelemetryInterceptor>();

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

        services.AddScoped<ClientOpEngine>(sp =>
            new ClientOpEngine(
                sp.GetRequiredService<IOperationDispatcher>(),
                sp.GetRequiredService<ILogger<ClientOpEngine>>(),
                sp.GetRequiredService<IStateApplier>()));
        services.AddScoped<ITemplateEngine>(sp => sp.GetRequiredService<ClientOpEngine>());

        // ─── State bridge: interpreters ────────────────────────────────
        services.AddScoped<OperationInterpreterRegistry<AppStore>>(sp =>
        {
            var registry = new OperationInterpreterRegistry<AppStore>(
                sp.GetRequiredService<ILogger<OperationInterpreterRegistry<AppStore>>>());
            registry.Add(new UiInterpreter());
            registry.Add(new AuthInterpreter());
            return registry;
        });

        services.AddScoped<AppStateApplier>();
        services.AddScoped<IStateApplier>(sp => sp.GetRequiredService<AppStateApplier>());

        // ─── Realtime + Chat client ────────────────────────────────────
        services.AddScoped<EjarRealtimeService>();
        services.AddScoped<UnreadService>();
        services.AddScoped<VersionPoll>();
        services.AddScoped<IChatClient, EjarChatClient>();

        return services;
    }
}
