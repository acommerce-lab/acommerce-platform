using ACommerce.Client.Http;
using ACommerce.Client.Http.Extensions;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Core;
using Ashare.Provider.Web.Components;
using Ashare.Provider.Web.Interpreters;
using Ashare.Provider.Web.Operations;
using Ashare.Provider.Web.Store;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ─── AppStore (حالة التطبيق — Scoped per circuit) ────────────────────
builder.Services.AddScoped<AppStore>();
builder.Services.AddScoped<ITemplateStore>(sp => sp.GetRequiredService<AppStore>());

// ─── OpEngine for client-side local operations (ui prefs) ─────────────
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// ─── ClientOpEngine + HttpDispatcher → Ashare.Api ─────────────────────
var apiBase = builder.Configuration["AshareApi:BaseUrl"] ?? "http://localhost:5500";
builder.Services.AddHttpClient("ashare", client =>
{
    client.BaseAddress = new Uri(apiBase);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Route registry: operation type → HTTP endpoint
var routeRegistry = new HttpRouteRegistry();
ProviderRoutes.Register(routeRegistry);
builder.Services.AddSingleton(routeRegistry);

// HttpDispatcher (IOperationDispatcher)
builder.Services.AddScoped<HttpDispatcher>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new HttpDispatcher(
        factory.CreateClient("ashare"),
        sp.GetRequiredService<HttpRouteRegistry>(),
        sp.GetRequiredService<OpEngine>(),
        sp.GetRequiredService<ILogger<HttpDispatcher>>());
});
builder.Services.AddScoped<IOperationDispatcher>(sp => sp.GetRequiredService<HttpDispatcher>());

// ApiReader: GET-only — reads don't need accounting entries
builder.Services.AddScoped<ApiReader>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new ApiReader(factory.CreateClient("ashare"), sp.GetRequiredService<AppStore>());
});

// ClientOpEngine — يُحقن IStateApplier لتطبيق جسر الحالة تلقائياً
builder.Services.AddScoped<ClientOpEngine>(sp =>
    new ClientOpEngine(
        sp.GetRequiredService<IOperationDispatcher>(),
        sp.GetRequiredService<ILogger<ClientOpEngine>>(),
        sp.GetRequiredService<IStateApplier>()));
builder.Services.AddScoped<ITemplateEngine>(sp => sp.GetRequiredService<ClientOpEngine>());

// ─── State Bridge: server operations → AppStore updates ──────────────
builder.Services.AddScoped<OperationInterpreterRegistry<AppStore>>(sp =>
{
    var registry = new OperationInterpreterRegistry<AppStore>(
        sp.GetRequiredService<ILogger<OperationInterpreterRegistry<AppStore>>>());
    registry.Add(new AuthInterpreter());
    registry.Add(new UiInterpreter());
    return registry;
});

builder.Services.AddScoped<AppStateApplier>();
builder.Services.AddScoped<IStateApplier>(sp => sp.GetRequiredService<AppStateApplier>());

// ─── Build ───────────────────────────────────────────────────────────
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
