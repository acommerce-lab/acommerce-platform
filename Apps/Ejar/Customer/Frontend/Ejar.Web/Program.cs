using ACommerce.Culture.Defaults;
using ACommerce.Kits.Versions.Templates;
using Ejar.Customer.UI;
using Ejar.Customer.UI.ClientHost;
using App = Ejar.Web.Components.App;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBase = builder.Configuration["EjarApi:BaseUrl"] ?? "http://localhost:5300";
var appVersion = builder.Configuration["App:Version"] ?? "1.0.0";
builder.Services.AddSingleton(new AppVersionInfo(Platform: "web", Version: appVersion));

// Bearer header يَأتي مِن AuthenticatedHttpClient (ClientHost.Auth) — لا
// AuthHeadersHandler. CultureHeadersHandler + AppVersionHeadersHandler يَبقَيان
// كَ message handlers لأنّهما لا يَحتاجان circuit-scoped state.
builder.Services.AddHttpClient("ejar", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<CultureHeadersHandler>()
.AddHttpMessageHandler<AppVersionHeadersHandler>();

// نُقطة دَخول واحدة لِكامِل قالَب Customer.Marketplace (V1's original UI).
builder.Services.AddEjarCustomer();

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Ejar.Customer.UI.Components.Routes).Assembly);
app.Run();
