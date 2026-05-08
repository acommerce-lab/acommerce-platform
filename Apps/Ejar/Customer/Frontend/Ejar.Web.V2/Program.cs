using ACommerce.Kits.Versions.Templates;
using Ejar.Customer.UI.V2.ClientHost;
using App = Ejar.Web.V2.Components.App;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBase = builder.Configuration["EjarApi:BaseUrl"] ?? "http://localhost:5300";
var appVersion = builder.Configuration["App:Version"] ?? "2.0.0";
builder.Services.AddSingleton(new AppVersionInfo(Platform: "web-v2", Version: appVersion));

// HttpClient "ejar" بسيط — Bearer يَتم على AuthenticatedHttpClient عبر
// DefaultRequestHeaders.Authorization. لا handler chain هنا.
builder.Services.AddHttpClient("ejar", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddEjarCustomerV2();

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Ejar.Customer.UI.V2.Components.Routes).Assembly);
app.Run();
