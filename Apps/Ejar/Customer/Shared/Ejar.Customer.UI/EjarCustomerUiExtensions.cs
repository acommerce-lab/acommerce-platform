using ACommerce.Chat.Client.Blazor;
using ACommerce.Client.Http;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
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

namespace Ejar.Customer.UI;

/// <summary>
/// تسجيل DI الموحَّد لكل خدمات عميل إيجار المشتركة. يُستدعى من كل مضيف
/// (Server / WebAssembly / MAUI) بعد تكوين <see cref="HttpClient"/> المسمّى
/// "ejar" بالـ BaseAddress الصحيح للبيئة الحالية.
/// </summary>
public static class EjarCustomerUiExtensions
{
    /// <summary>
    /// يضيف خدمات الـ UI المشتركة. <strong>المتطلب المسبق</strong>:
    /// المضيف يسجّل <c>HttpClient</c> مسمّى "ejar" بـ BaseAddress
    /// مناسب (مثلاً <c>http://localhost:5300</c> للتطوير،
    /// <c>https://api.ejar.ye</c> للإنتاج). تتعامل المكتبة معه فقط
    /// عبر <see cref="IHttpClientFactory"/>.
    /// </summary>
    public static IServiceCollection AddEjarCustomerUI(this IServiceCollection services)
    {
        // ─── Store + Translations ──────────────────────────────────────
        services.AddScoped<AppStore>();
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
        services.AddScoped<EjarCircuitHttp>();

        // ─── Versions Kit (frontend) ───────────────────────────────────
        // المضيف يجب أن يسجّل AppVersionInfo singleton قبل استدعاء هذه الدالّة
        // ويضيف AppVersionHeadersHandler على HttpClient "ejar" عبر AddHttpMessageHandler.
        services.AddVersionsTemplates(httpClientName: "ejar");

        // ─── Subscriptions Kit (frontend) ─────────────────────────────
        // SubscriptionState يُحدَّث من التطبيق بعد التحقّق من الاشتراك.
        services.AddSubscriptionsTemplates();

        // ─── OpEngine للعمليات المحلّية ────────────────────────────────
        services.AddScoped<OpEngine>(sp =>
            new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

        // ─── Routes registry + dispatcher + reader ────────────────────
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

        services.AddBlazorChatClient(opts =>
        {
            opts.HttpClientName    = "ejar";
            opts.EnterPathTemplate = "/chat/{convId}/enter";
            opts.LeavePathTemplate = "/chat/{convId}/leave";
            opts.SendPathTemplate  = "/conversations/{convId}/messages";
        });

        return services;
    }
}
