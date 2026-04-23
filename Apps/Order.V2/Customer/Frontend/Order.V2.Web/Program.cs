using ACommerce.Client.Http;
using ACommerce.Client.Http.Extensions;
using ACommerce.Culture.Blazor;
using ACommerce.Client.Operations;
using ACommerce.Client.Operations.Interceptors;
using ACommerce.Client.StateBridge;
using ACommerce.L10n.Blazor;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.OperationEngine.Interceptors.Extensions;
using Order.V2.Web.Components;
using Order.V2.Web.Interpreters;
using Order.V2.Web.Operations;
using Order.V2.Web.Services;
using Order.V2.Web.Store;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddBlazorCultureStack();

builder.Services.AddScoped<AppStore>();
builder.Services.AddScoped<ITemplateStore>(sp => sp.GetRequiredService<AppStore>());

builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

var apiBase = builder.Configuration["OrderApi:BaseUrl"] ?? "http://localhost:5102";
builder.Services.AddHttpClient("order", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
});

var routeRegistry = new HttpRouteRegistry();
OrderRoutes.Register(routeRegistry);
builder.Services.AddSingleton(routeRegistry);

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

builder.Services.AddScoped<ApiReader>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new ApiReader(factory.CreateClient("order"), sp.GetRequiredService<AppStore>());
});

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
    registry.Add(new CartInterpreter());
    registry.Add(new UiInterpreter());
    return registry;
});

builder.Services.AddScoped<AppStateApplier>();
builder.Services.AddScoped<IStateApplier>(sp => sp.GetRequiredService<AppStateApplier>());

builder.Services.AddScoped<AuthStateService>();

builder.Services.AddEmbeddedL10n<CustomerTranslations, AppLangContext>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
