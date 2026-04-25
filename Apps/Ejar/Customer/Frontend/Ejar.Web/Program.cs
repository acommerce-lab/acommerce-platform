using ACommerce.Chat.Client.Blazor;
using ACommerce.Client.Http;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.Culture.Abstractions;
using ACommerce.Culture.Defaults;
using ACommerce.OperationEngine.Core;
using Ejar.Web.Components;
using Ejar.Web.Interceptors;
using Ejar.Web.Interpreters;
using Ejar.Web.Operations;
using Ejar.Web.Services;
using Ejar.Web.Store;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ─── AppStore (ITemplateStore) + L (translations) ────────────────────
builder.Services.AddScoped<AppStore>();
builder.Services.AddScoped<ITemplateStore>(sp => sp.GetRequiredService<AppStore>());
builder.Services.AddSingleton<ITranslationProvider, EmbeddedTranslationProvider>();
builder.Services.AddScoped<L>();

builder.Services.AddScoped<ITimezoneProvider, JsTimezoneProvider>();
builder.Services.AddSingleton<INumeralNormalizer, DefaultNumeralNormalizer>();

builder.Services.AddScoped<CultureInterceptor>();
builder.Services.AddTransient<CultureHeadersHandler>();
builder.Services.AddScoped<EjarCircuitHttp>();

// ─── OpEngine للعمليات المحلّية ────────────────────────────────────────
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// ─── HTTP → Ejar.Api ──────────────────────────────────────────────────
var apiBase = builder.Configuration["EjarApi:BaseUrl"] ?? "http://localhost:5300";
builder.Services.AddHttpClient("ejar", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<CultureHeadersHandler>();

var routeRegistry = new HttpRouteRegistry();
EjarRoutes.Register(routeRegistry);
builder.Services.AddSingleton(routeRegistry);

builder.Services.AddScoped<HttpDispatcher>(sp =>
{
    var circuit = sp.GetRequiredService<EjarCircuitHttp>();
    return new HttpDispatcher(
        circuit.Client,
        sp.GetRequiredService<HttpRouteRegistry>(),
        sp.GetRequiredService<OpEngine>(),
        sp.GetRequiredService<ILogger<HttpDispatcher>>());
});
builder.Services.AddScoped<IOperationDispatcher>(sp => sp.GetRequiredService<HttpDispatcher>());

builder.Services.AddScoped<ApiReader>(sp =>
{
    var circuit = sp.GetRequiredService<EjarCircuitHttp>();
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

// ─── State bridge: interpreters ───────────────────────────────────────
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
builder.Services.AddScoped<EjarRealtimeService>();

// Chat client uses the named "ejar" HttpClient so calls go to the API.
// Backend exposes /chat/{convId}/enter and /leave (registered in CatalogController);
// send falls back to the existing /conversations/{convId}/messages endpoint.
builder.Services.AddBlazorChatClient(opts =>
{
    opts.HttpClientName    = "ejar";
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
