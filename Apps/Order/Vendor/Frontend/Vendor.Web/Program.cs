using ACommerce.Client.Http;
using ACommerce.Client.Http.Extensions;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.OperationEngine.Interceptors.Extensions;
using Vendor.Web.Components;
using Vendor.Web.Interpreters;
using Vendor.Web.Operations;
using Vendor.Web.Services;
using Vendor.Web.Store;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ─── AppStore (حالة التطبيق — Scoped per circuit) ────────────────────
builder.Services.AddScoped<AppStore>();
builder.Services.AddScoped<ITemplateStore>(sp => sp.GetRequiredService<AppStore>());

// ─── OpEngine for client-side local operations (ui prefs) ───────────
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// ─── ClientOpEngine + HttpDispatcher → Order.Api ─────────────────────
var orderApiBase = builder.Configuration["OrderApi:BaseUrl"] ?? "http://localhost:5101";
builder.Services.AddHttpClient("order-api", c =>
{
    c.BaseAddress = new Uri(orderApiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
});

// Route registry: operation type → HTTP endpoint
var routeRegistry = new HttpRouteRegistry();
VendorRoutes.Register(routeRegistry);
builder.Services.AddSingleton(routeRegistry);

// HttpDispatcher (IOperationDispatcher)
builder.Services.AddScoped<HttpDispatcher>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new HttpDispatcher(
        factory.CreateClient("order-api"),
        sp.GetRequiredService<HttpRouteRegistry>(),
        sp.GetRequiredService<OpEngine>(),
        sp.GetRequiredService<ILogger<HttpDispatcher>>());
});
builder.Services.AddScoped<IOperationDispatcher>(sp => sp.GetRequiredService<HttpDispatcher>());

// ApiReader: GET-only for Order.Api
builder.Services.AddScoped<ApiReader>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new ApiReader(factory.CreateClient("order-api"), sp.GetRequiredService<AppStore>());
});

// ClientOpEngine — يُحقن IStateApplier لتطبيق جسر الحالة تلقائياً
builder.Services.AddScoped<ClientOpEngine>(sp =>
    new ClientOpEngine(
        sp.GetRequiredService<IOperationDispatcher>(),
        sp.GetRequiredService<ILogger<ClientOpEngine>>(),
        sp.GetRequiredService<IStateApplier>()));
builder.Services.AddScoped<ITemplateEngine>(sp => sp.GetRequiredService<ClientOpEngine>());

// ─── HTTP Client → Vendor.Api (orders, settings, schedule) ────────────
var vendorApiBase = builder.Configuration["VendorApi:BaseUrl"] ?? "http://localhost:5201";
builder.Services.AddHttpClient("vendor-api", c =>
{
    c.BaseAddress = new Uri(vendorApiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<VendorApiClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new VendorApiClient(factory.CreateClient("vendor-api"), sp.GetRequiredService<AppStore>());
});

// ─── OrderApiClient for direct calls to Order.Api ────────────────────
builder.Services.AddScoped<OrderApiClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new OrderApiClient(factory.CreateClient("order-api"), sp.GetRequiredService<AppStore>());
});

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
