using ACommerce.Client.Http;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Core;
using Ejar.Web.Components;
using Ejar.Web.Interceptors;
using Ejar.Web.Interpreters;
using Ejar.Web.Operations;
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

builder.Services.AddScoped<CultureInterceptor>();
builder.Services.AddTransient<CultureHeadersHandler>();

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
    var f = sp.GetRequiredService<IHttpClientFactory>();
    return new HttpDispatcher(
        f.CreateClient("ejar"),
        sp.GetRequiredService<HttpRouteRegistry>(),
        sp.GetRequiredService<OpEngine>(),
        sp.GetRequiredService<ILogger<HttpDispatcher>>());
});
builder.Services.AddScoped<IOperationDispatcher>(sp => sp.GetRequiredService<HttpDispatcher>());

builder.Services.AddScoped<ApiReader>(sp =>
{
    var f = sp.GetRequiredService<IHttpClientFactory>();
    return new ApiReader(
        f.CreateClient("ejar"),
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
