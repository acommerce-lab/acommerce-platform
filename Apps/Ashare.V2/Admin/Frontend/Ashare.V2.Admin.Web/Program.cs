using ACommerce.Client.Http;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.OperationEngine.Core;
using Ashare.V2.Admin.Web.Components;
using Ashare.V2.Admin.Web.Interceptors;
using Ashare.V2.Admin.Web.Interpreters;
using Ashare.V2.Admin.Web.Operations;
using Ashare.V2.Admin.Web.Services;
using Ashare.V2.Admin.Web.Store;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddScoped<AppStore>();
builder.Services.AddScoped<ITemplateStore>(sp => sp.GetRequiredService<AppStore>());

builder.Services.AddScoped<AdminCircuitHttp>();

builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

var apiBase = builder.Configuration["AshareV2AdminApi:BaseUrl"] ?? "http://localhost:5503";
builder.Services.AddHttpClient("ashare-v2-admin", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout     = TimeSpan.FromSeconds(30);
});

var routeRegistry = new HttpRouteRegistry();
AdminV2Routes.Register(routeRegistry);
builder.Services.AddSingleton(routeRegistry);

builder.Services.AddScoped<HttpDispatcher>(sp =>
{
    var circuit = sp.GetRequiredService<AdminCircuitHttp>();
    return new HttpDispatcher(
        circuit.Client,
        sp.GetRequiredService<HttpRouteRegistry>(),
        sp.GetRequiredService<OpEngine>(),
        sp.GetRequiredService<ILogger<HttpDispatcher>>());
});
builder.Services.AddScoped<IOperationDispatcher>(sp => sp.GetRequiredService<HttpDispatcher>());

builder.Services.AddScoped<ApiReader>(sp => new ApiReader(sp.GetRequiredService<AdminCircuitHttp>()));

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

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
