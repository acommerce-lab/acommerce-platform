using ACommerce.Client.Http;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.L10n.Blazor;
using ACommerce.OperationEngine.Core;
using Ashare.V2.Provider.Web.Components;
using Ashare.V2.Provider.Web.Interceptors;
using Ashare.V2.Provider.Web.Interpreters;
using Ashare.V2.Provider.Web.Operations;
using Ashare.V2.Provider.Web.Services;
using Ashare.V2.Provider.Web.Store;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddScoped<AppStore>();
builder.Services.AddScoped<ITemplateStore>(sp => sp.GetRequiredService<AppStore>());

builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

var apiBase = builder.Configuration["AshareV2Api:BaseUrl"] ?? "http://localhost:5600";
builder.Services.AddHttpClient("ashare-v2", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout     = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<ProviderCircuitHttp>();
builder.Services.AddScoped<ApiReader>();

var routeRegistry = new HttpRouteRegistry();
ProviderV2Routes.Register(routeRegistry);
builder.Services.AddSingleton(routeRegistry);

builder.Services.AddScoped<HttpDispatcher>(sp =>
{
    var circuit = sp.GetRequiredService<ProviderCircuitHttp>();
    return new HttpDispatcher(
        circuit.Client,
        sp.GetRequiredService<HttpRouteRegistry>(),
        sp.GetRequiredService<OpEngine>(),
        sp.GetRequiredService<ILogger<HttpDispatcher>>());
});
builder.Services.AddScoped<IOperationDispatcher>(sp => sp.GetRequiredService<HttpDispatcher>());

builder.Services.AddScoped<ClientOpEngine>(sp =>
    new ClientOpEngine(
        sp.GetRequiredService<IOperationDispatcher>(),
        sp.GetRequiredService<ILogger<ClientOpEngine>>(),
        sp.GetRequiredService<IStateApplier>()));
builder.Services.AddScoped<ITemplateEngine>(sp => sp.GetRequiredService<ClientOpEngine>());

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
builder.Services.AddScoped<AuthStateService>();

builder.Services.AddEmbeddedL10n<ProviderTranslations, AppLangContext>();

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
