using ACommerce.Chat.Client.Blazor;
using ACommerce.Client.Http;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.Culture.Abstractions;
using ACommerce.Culture.Defaults;
using ACommerce.OperationEngine.Core;
using Ashare.V2.Web.Components;
using Ashare.V2.Web.Interceptors;
using Ashare.V2.Web.Interpreters;
using Ashare.V2.Web.Operations;
using Ashare.V2.Web.Services;
using Ashare.V2.Web.Store;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ─── AppStore (ITemplateStore) + L (translations) ────────────────────
builder.Services.AddScoped<AppStore>();
builder.Services.AddScoped<ITemplateStore>(sp => sp.GetRequiredService<AppStore>());
// ── Translation — ProviderContract (عقد) + Implementation مضمَّنة.
//      يسمح باستبداله لاحقاً بـ ApiTranslationProvider أو ResxTranslationProvider.
builder.Services.AddSingleton<ITranslationProvider, EmbeddedTranslationProvider>();
builder.Services.AddScoped<L>();

// ── ProviderContract للتوقيت (مُبقى للصياغة النسبيّة — Tz.FormatRelative).
//    التحويل الأساسيّ انتقل إلى CultureInterceptor الذي يستعمل Culture.TimeZone.
builder.Services.AddSingleton<INumeralNormalizer, DefaultNumeralNormalizer>();
builder.Services.AddScoped<ITimezoneProvider, JsTimezoneProvider>();

// ── معترض الثقافة (إياب): يطبّق Culture على حمولات OperationEnvelope
//    (DateTime بحسب Culture.TimeZone، وعملات/ترجمات hooks).
builder.Services.AddScoped<CultureInterceptor>();

// ── معترض الثقافة (ذهاب): DelegatingHandler يختم كلّ طلب برؤوس Culture.
builder.Services.AddTransient<CultureHeadersHandler>();
builder.Services.AddScoped<Ashare2CircuitHttp>();

// ─── OpEngine للعمليات المحلّية ────────────────────────────────────────
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// ─── HTTP → Ashare.V2.Api (client-side dispatcher) ──────────────────
// ملاحظة: هذه هي الطبقة الوحيدة التي تلمس HttpClient. كلّ الصفحات تبني
// Operation ثم Engine.DispatchAsync، و HttpDispatcher يحوّلها إلى طلب.
var apiBase = builder.Configuration["AshareV2Api:BaseUrl"] ?? "http://localhost:5600";
builder.Services.AddHttpClient("ashare-v2", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
})
// CultureHeadersHandler يضيف Accept-Language / X-User-Timezone / X-User-Currency
// على كلّ طلب صادر — الخدمة الخلفيّة تفهم سياق ثقافة المستخدم.
.AddHttpMessageHandler<CultureHeadersHandler>();

var routeRegistry = new HttpRouteRegistry();
V2Routes.Register(routeRegistry);
builder.Services.AddSingleton(routeRegistry);

builder.Services.AddScoped<HttpDispatcher>(sp =>
{
    var circuit = sp.GetRequiredService<Ashare2CircuitHttp>();
    return new HttpDispatcher(
        circuit.Client,
        sp.GetRequiredService<HttpRouteRegistry>(),
        sp.GetRequiredService<OpEngine>(),
        sp.GetRequiredService<ILogger<HttpDispatcher>>());
});
builder.Services.AddScoped<IOperationDispatcher>(sp => sp.GetRequiredService<HttpDispatcher>());

builder.Services.AddScoped<ApiReader>(sp =>
{
    var circuit = sp.GetRequiredService<Ashare2CircuitHttp>();
    return new ApiReader(
        circuit.Client,
        sp.GetRequiredService<CultureInterceptor>());
});

builder.Services.AddScoped<ClientOpEngine>(sp =>
    new ClientOpEngine(
        sp.GetRequiredService<IOperationDispatcher>(),
        sp.GetRequiredService<ILogger<ClientOpEngine>>(),
        sp.GetRequiredService<IStateApplier>()));
builder.Services.AddScoped<ITemplateEngine>(sp => sp.GetRequiredService<ClientOpEngine>());

// ─── State bridge: interpreters مُسجَّلة ─────────────────────────────
// كلّ interpreter يتفاعل مع نوع عمليّة معيّن ويُحدّث AppStore تفاؤليّاً.
builder.Services.AddScoped<OperationInterpreterRegistry<AppStore>>(sp =>
{
    var registry = new OperationInterpreterRegistry<AppStore>(
        sp.GetRequiredService<ILogger<OperationInterpreterRegistry<AppStore>>>());
    registry.Add(new UiInterpreter());
    registry.Add(new AuthInterpreter());
    return registry;
});

builder.Services.AddScoped<AppStateApplier>();
builder.Services.AddScoped<IStateApplier>(sp => sp.GetRequiredService<AppStateApplier>());

// ─── Realtime client + Chat client ────────────────────────────────────
builder.Services.AddScoped<Ashare2RealtimeService>();
builder.Services.AddBlazorChatClient(opts =>
{
    opts.HttpClientName    = "ashare-v2";
    opts.EnterPathTemplate = "/chat/{convId}/enter";
    opts.LeavePathTemplate = "/chat/{convId}/leave";
    opts.SendPathTemplate  = "/conversations/{convId}/messages";
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
