using ACommerce.Kits.Versions.Templates;
using Ejar.Customer.UI;
using Ejar.Customer.UI.Interceptors;
using Ejar.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// HTTP → Ejar.Api. ضع BaseUrl عبر appsettings ({ "EjarApi": { "BaseUrl": ... } })
// — يبدّل بين localhost للتطوير و https://api.ejar.ye للإنتاج.
var apiBase = builder.Configuration["EjarApi:BaseUrl"] ?? "http://localhost:5300";

// إصدار التطبيق الحاليّ — يُرسَل في رأس X-App-Version لكلّ طلب
// عبر AppVersionHeadersHandler، ويُستخدم كذلك للفحص الأوّليّ في AcVersionGate.
var appVersion = builder.Configuration["App:Version"] ?? "1.0.0";
builder.Services.AddSingleton(new AppVersionInfo(Platform: "web", Version: appVersion));

builder.Services.AddHttpClient("ejar", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<CultureHeadersHandler>()
.AddHttpMessageHandler<AppVersionHeadersHandler>();

// كل خدمات الـ UI المشتركة (AppStore, OpEngine, dispatchers, chat client …)
builder.Services.AddEjarCustomerUI();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(EjarCustomerUiExtensions).Assembly);

app.Run();
