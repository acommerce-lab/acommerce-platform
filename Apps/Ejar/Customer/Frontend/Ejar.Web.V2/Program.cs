using ACommerce.Kits.Versions.Templates;
using Ejar.Customer.UI;                        // V1 services + AppVersionInfo
using Ejar.Customer.UI.Interceptors;
using Ejar.Customer.UI.V2.ClientHost;          // V2 host
using App = Ejar.Customer.UI.V2.Components.App;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// HTTP → Ejar.Api (نفس API V1)
var apiBase = builder.Configuration["EjarApi:BaseUrl"] ?? "http://localhost:5300";
var appVersion = builder.Configuration["App:Version"] ?? "2.0.0";
builder.Services.AddSingleton(new AppVersionInfo(Platform: "web-v2", Version: appVersion));

builder.Services.AddHttpClient("ejar", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<CultureHeadersHandler>()
.AddHttpMessageHandler<AppVersionHeadersHandler>()
.AddHttpMessageHandler<AuthHeadersHandler>();

// V2 يَستدعي AddEjarCustomerUI داخلياً + يَربط kit stores.
builder.Services.AddEjarCustomerV2();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

// Routes.razor (في Ejar.Customer.UI.V2 — نفس assembly App) يَحوي
// @page "/" + @page "/{*Path:nonfile}". اكتشاف App's assembly كافٍ.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
