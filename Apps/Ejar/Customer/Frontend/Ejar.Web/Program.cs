using ACommerce.Kits.Versions.Templates;
using Ejar.Customer.UI;
using Ejar.Customer.UI.ClientHost;
using Ejar.Customer.UI.Interceptors;
using App = Ejar.Web.Components.App;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBase = builder.Configuration["EjarApi:BaseUrl"] ?? "http://localhost:5300";
var appVersion = builder.Configuration["App:Version"] ?? "1.0.0";
builder.Services.AddSingleton(new AppVersionInfo(Platform: "web", Version: appVersion));

builder.Services.AddHttpClient("ejar", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<CultureHeadersHandler>()
.AddHttpMessageHandler<AppVersionHeadersHandler>()
.AddHttpMessageHandler<AuthHeadersHandler>();

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
