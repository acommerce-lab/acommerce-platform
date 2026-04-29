using ACommerce.Kits.Versions.Templates;
using Ejar.Customer.UI;
using Ejar.Customer.UI.Components.Layout;
using Ejar.Customer.UI.Interceptors;
using Ejar.Customer.UI.Store;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// نقطة جذر المكوّنات. WASM لا يحتاج <Routes> إضافي — نُحمِّل MainLayout
// مع Router داخلي عبر <App> لا. الأبسط: نضع Routes مباشرةً.
builder.RootComponents.Add<Ejar.Customer.UI.Components.Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// إصدار التطبيق الحاليّ — يُرسَل في رأس X-App-Version لكلّ طلب عبر
// AppVersionHeadersHandler، ويُستخدم في AcAppVersionGate.
// المتطلَّب من ACommerce.Kits.Versions.Templates: AppVersionInfo singleton
// مسجَّل قبل AddEjarCustomerUI() الذي يستهلك VersionState + AppVersionHeadersHandler.
var appVersion = builder.Configuration["App:Version"] ?? "1.0.0";
builder.Services.AddSingleton(new AppVersionInfo(Platform: "wasm", Version: appVersion));

// HTTP → Ejar.Api. BaseAddress يأتي من appsettings.json (مع override
// عبر appsettings.Development.json) — نفس آلية Ejar.Web بالضبط.
var apiBase = builder.Configuration["EjarApi:BaseUrl"]
              ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddHttpClient("ejar", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<CultureHeadersHandler>()
.AddHttpMessageHandler<AppVersionHeadersHandler>()
.AddHttpMessageHandler<AuthHeadersHandler>();

// HttpClient الافتراضي (للمكوّنات التي تحقن HttpClient بدون اسم)
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("ejar"));

builder.Services.AddEjarCustomerUI();

// استعِد الحالة من localStorage قبل أيّ render — يضمن أنّ JWT والمفضّلات تكون
// جاهزة قبل أن يقيّم Router سمات [Authorize] على الصفحات. وإلّا تظهر صفحة
// Login لحظات ثمّ تختفي، أو يُعاد توجيه المستخدم إلى الرئيسيّة من صفحة محميّة
// رغم أنّ التوكن محفوظ.
var host = builder.Build();
await host.Services.GetRequiredService<AppStorePersistence>().RestoreAsync();
await host.RunAsync();
