using ACommerce.Client.Http;
using ACommerce.Client.Http.Extensions;
using ACommerce.Culture.Blazor;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.OperationEngine.Interceptors.Extensions;
using Order.Web.Components;
using Order.Web.Interpreters;
using Order.Web.Operations;
using Order.Web.Services;
using Order.Web.Store;

var builder = WebApplication.CreateBuilder(args);

// Enable static web assets in ALL environments (default-enables only in Dev).
// Without this, _content/<RclLib>/* returns 404 at runtime.
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Culture stack — per-circuit browser-derived TZ + numeral system, used to
// render timestamps (chat, orders) in the viewing user's local time.
builder.Services.AddBlazorCultureStack();

// ─── AppStore (حالة التطبيق — Scoped per circuit) ────────────────────
builder.Services.AddScoped<AppStore>();
builder.Services.AddScoped<ITemplateStore>(sp => sp.GetRequiredService<AppStore>());

// ─── OpEngine for client-side local operations (cart, ui prefs) ──────
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// ─── ClientOpEngine + HttpDispatcher → Order.Api ─────────────────────
var apiBase = builder.Configuration["OrderApi:BaseUrl"] ?? "http://localhost:5101";
builder.Services.AddHttpClient("order", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
});

// Route registry: operation type → HTTP endpoint
var routeRegistry = new HttpRouteRegistry();
OrderRoutes.Register(routeRegistry);
builder.Services.AddSingleton(routeRegistry);

// HttpDispatcher (IOperationDispatcher)
builder.Services.AddScoped<HttpDispatcher>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new HttpDispatcher(
        factory.CreateClient("order"),
        sp.GetRequiredService<HttpRouteRegistry>(),
        sp.GetRequiredService<OpEngine>(),
        sp.GetRequiredService<ILogger<HttpDispatcher>>());
});
builder.Services.AddScoped<IOperationDispatcher>(sp => sp.GetRequiredService<HttpDispatcher>());

// ApiReader: GET-only — reads don't need accounting entries
builder.Services.AddScoped<ApiReader>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new ApiReader(factory.CreateClient("order"), sp.GetRequiredService<AppStore>());
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
    registry.Add(new CartInterpreter());
    registry.Add(new UiInterpreter());
    return registry;
});

builder.Services.AddScoped<AppStateApplier>();
builder.Services.AddScoped<IStateApplier>(sp => sp.GetRequiredService<AppStateApplier>());

// ─── Auth persistence (ProtectedLocalStorage) ───────────────────────
builder.Services.AddScoped<AuthStateService>();

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
