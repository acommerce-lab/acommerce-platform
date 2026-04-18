using ACommerce.Client.Http;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Core;
using Ashare.V2.Web.Components;
using Ashare.V2.Web.Operations;
using Ashare.V2.Web.Store;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ─── AppStore (ITemplateStore) + L (translations) ────────────────────
builder.Services.AddScoped<AppStore>();
builder.Services.AddScoped<ITemplateStore>(sp => sp.GetRequiredService<AppStore>());
builder.Services.AddScoped<L>();
builder.Services.AddScoped<TimezoneService>();

// ─── OpEngine للعمليات المحلّية ────────────────────────────────────────
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// ─── HTTP → Ashare.V2.Api ─────────────────────────────────────────────
var apiBase = builder.Configuration["AshareV2Api:BaseUrl"] ?? "http://localhost:5600";
builder.Services.AddHttpClient("ashare-v2", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
});
// alias — الصفحات تستعمل "api" بالاسم المختصر للـ PostAsJson/Put
builder.Services.AddHttpClient("api", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
});

var routeRegistry = new HttpRouteRegistry();
V2Routes.Register(routeRegistry);
builder.Services.AddSingleton(routeRegistry);

builder.Services.AddScoped<HttpDispatcher>(sp =>
{
    var f = sp.GetRequiredService<IHttpClientFactory>();
    return new HttpDispatcher(
        f.CreateClient("ashare-v2"),
        sp.GetRequiredService<HttpRouteRegistry>(),
        sp.GetRequiredService<OpEngine>(),
        sp.GetRequiredService<ILogger<HttpDispatcher>>());
});
builder.Services.AddScoped<IOperationDispatcher>(sp => sp.GetRequiredService<HttpDispatcher>());

builder.Services.AddScoped<ApiReader>(sp =>
{
    var f = sp.GetRequiredService<IHttpClientFactory>();
    return new ApiReader(f.CreateClient("ashare-v2"));
});

builder.Services.AddScoped<ClientOpEngine>(sp =>
    new ClientOpEngine(
        sp.GetRequiredService<IOperationDispatcher>(),
        sp.GetRequiredService<ILogger<ClientOpEngine>>(),
        sp.GetRequiredService<IStateApplier>()));
builder.Services.AddScoped<ITemplateEngine>(sp => sp.GetRequiredService<ClientOpEngine>());

// ─── State bridge (فارغ حالياً — لا مُفسّرات في شريحة Home) ───────────
builder.Services.AddScoped<OperationInterpreterRegistry<AppStore>>(sp =>
    new OperationInterpreterRegistry<AppStore>(
        sp.GetRequiredService<ILogger<OperationInterpreterRegistry<AppStore>>>()));

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
