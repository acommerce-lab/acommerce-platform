using ACommerce.Client.Http;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.ClientHost.Auth;
using ACommerce.OperationEngine.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACommerce.ClientHost.Operations;

/// <summary>
/// تَسجيل DI لِـ OAM client engine — العَصَب المُحاسبيّ في طَبَقَة العَميل.
/// كلّ <c>Default&lt;Kit&gt;Store</c> في الكيتس يَستَخدِم
/// <see cref="ITemplateEngine"/> لِإرسال operations كَقُيود محاسبيّة،
/// فتَستَطيع compositions حَقن مُعتَرضات (telemetry, retry-on-401,
/// realtime broadcast) بدون لَمس الكيتس أو التَطبيق.
///
/// <para>المُتَطَلَّب المُسَبَّق: <c>AddClientAuth</c> مُسَجَّلَة (تُوَفِّر
/// <see cref="AuthenticatedHttpClient"/>)، أَو HttpClient آخَر يَتم تَمريره
/// عَبر factory.</para>
///
/// <para>تَطبيق نَموذجيّ:
/// <code>
/// services.AddClientAuth(o =&gt; { ... });
/// services.AddClientOpEngine();
/// services.AddChatRoutes();        // مِن kit
/// services.AddListingsRoutes();    // مِن kit
/// // …
/// </code></para>
/// </summary>
public static class ClientOpEngineExtensions
{
    public static IServiceCollection AddClientOpEngine(this IServiceCollection services)
    {
        // OpEngine = مُحَرِّك OAM يَلفّ <c>http.send</c> transport ops.
        // مُسَجَّل مرّة واحدة. interceptors عَلى مُستوى الـ engine تَنطَبِق
        // عَلى كلّ dispatch (telemetry، retry، realtime broadcast، …).
        services.AddScoped<OpEngine>(sp =>
            new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

        // RouteRegistry — singleton يَستَخلِص routes كلّ الكيتس المُسَجَّلة
        // عَبر <see cref="IRoutesRegistrar"/>. الكيتس تَستَدعي
        // <c>Add&lt;Kit&gt;Routes</c> فيُسَجِّلون تَنفيذ، وهذا الـ factory
        // يَجمَعهم في registry واحِد عِند أَوّل طَلَب.
        services.AddSingleton<HttpRouteRegistry>(sp =>
        {
            var registry = new HttpRouteRegistry();
            foreach (var registrar in sp.GetServices<IRoutesRegistrar>())
                registrar.Register(registry);
            return registry;
        });

        // HttpDispatcher — يَلفّ HttpClient + RouteRegistry + OpEngine.
        // يَستَخدِم AuthenticatedHttpClient (مِن AddClientAuth) فيَخرُج Bearer
        // token تلقائيّاً.
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

        // ClientOpEngine — الواجهة الأُولى التي يَستَهلِكها كلّ kit Store.
        // مُسَجَّل بدون IStateApplier — كلّ Store يُطَبِّق state داخِليّاً
        // مِن الـ envelope. compositions تَحتاج state coordination تُسَجِّل
        // IStateApplier إضافيّاً.
        services.AddScoped<ClientOpEngine>(sp =>
            new ClientOpEngine(
                sp.GetRequiredService<IOperationDispatcher>(),
                sp.GetRequiredService<ILogger<ClientOpEngine>>(),
                sp.GetService<IStateApplier>()));
        services.AddScoped<ITemplateEngine>(sp => sp.GetRequiredService<ClientOpEngine>());

        return services;
    }
}
