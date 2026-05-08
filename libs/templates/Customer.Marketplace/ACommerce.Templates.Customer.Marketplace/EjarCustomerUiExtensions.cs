using ACommerce.Chat.Client.Blazor;
using ACommerce.Client.Http;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.ClientHost.Auth;
using ACommerce.ClientHost.KitApi;
using ACommerce.Culture.Abstractions;
using ACommerce.Culture.Defaults;
using ACommerce.Kits.Versions.Templates;
using ACommerce.OperationEngine.Core;
using ACommerce.Subscriptions.Templates.Extensions;
using ACommerce.Templates.Shared.Models;
using Ejar.Customer.UI.Interceptors;
using Ejar.Customer.UI.Services;
using Ejar.Customer.UI.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ejar.Customer.UI;

/// <summary>
/// تَسجيل DI الموحَّد لِكلّ خَدَمات قالَب Customer.Marketplace. يُسْتَدعى
/// مِن المُضيف بَعد تَسجيل <see cref="HttpClient"/> المُسَمّى "ejar".
///
/// <para>F57: مَجموعَة Auth انتَقَلَت إلى <c>ClientHost.Auth</c> — لا
/// EjarAuthenticationStateProvider، لا AuthHeadersHandler، لا EjarCircuitHttp،
/// لا AuthInterpreter بَعد اليَوم. <c>AuthenticatedHttpClient</c> +
/// <c>ClientAuthStateProvider</c> + <c>LocalStorageClientAuthPersistence</c>
/// يَتَوَلَّون كلّ شَيء.</para>
/// </summary>
public static class EjarCustomerUiExtensions
{
    public static IServiceCollection AddEjarCustomerUI(this IServiceCollection services)
    {
        // ─── Auth machinery (ClientHost.Auth) ──────────────────────────
        // مَفتاح localStorage = "ejar.auth" (تَوافُق مَع V1 OLD لِئلّا تَخرُج
        // الجَلسات الحاليّة). scheme = "EjarAuth".
        services.AddClientAuth(o =>
        {
            o.HttpClientName = "ejar";
            o.StorageKey     = "ejar.auth";
            o.Scheme         = "EjarAuth";
        });

        // ─── Store + Translations ──────────────────────────────────────
        services.AddScoped<AppStore>();
        services.AddScoped<AppStorePersistence>();
        services.AddScoped<ITemplateStore>(sp => sp.GetRequiredService<AppStore>());

        services.AddSingleton<ITranslationProvider, EmbeddedTranslationProvider>();
        services.AddScoped<L>();

        services.AddScoped<ITimezoneProvider, JsTimezoneProvider>();
        services.AddSingleton<INumeralNormalizer, DefaultNumeralNormalizer>();

        // ─── HTTP interceptors المُتَبَقّية ────────────────────────────
        services.AddScoped<CultureInterceptor>();
        services.AddTransient<CultureHeadersHandler>();

        // ─── Versions Kit (frontend) ───────────────────────────────────
        services.AddVersionsTemplates(httpClientName: "ejar");

        // ─── Subscriptions Kit (frontend) ─────────────────────────────
        services.AddSubscriptionsTemplates();

        // ─── OpEngine للعَمَليّات المَحَلّيّة ──────────────────────────
        services.AddScoped<OpEngine>(sp =>
            new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

        // ─── Routes registry + dispatcher + reader ────────────────────
        services.AddSingleton(new HttpRouteRegistry());

        services.AddScoped<HttpDispatcher>(sp =>
        {
            var http = sp.GetRequiredService<AuthenticatedHttpClient>();
            return new HttpDispatcher(
                http.Client,
                sp.GetRequiredService<HttpRouteRegistry>(),
                sp.GetRequiredService<OpEngine>(),
                sp.GetRequiredService<ILogger<HttpDispatcher>>());
        });
        services.AddScoped<IOperationDispatcher>(sp => sp.GetRequiredService<HttpDispatcher>());

        services.AddScoped<ApiReader>(sp =>
        {
            var http = sp.GetRequiredService<AuthenticatedHttpClient>();
            return new ApiReader(http.Client, sp.GetRequiredService<CultureInterceptor>());
        });

        services.AddScoped<FavoritesSync>();
        services.AddScoped<FirebasePushService>();

        // ─── KitApi pipeline موحَّد ─────────────────────────────────────
        services.AddKitApiPipeline(sp =>
            sp.GetRequiredService<AuthenticatedHttpClient>().Client);

        // ─── kit ApiClients (per-kit shape ownership) ──────────────────
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

        // ─── State bridge: empty registry (AuthInterpreter كان dead في F57) ──
        services.AddScoped<OperationInterpreterRegistry<AppStore>>(sp =>
            new OperationInterpreterRegistry<AppStore>(
                sp.GetRequiredService<ILogger<OperationInterpreterRegistry<AppStore>>>()));

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
