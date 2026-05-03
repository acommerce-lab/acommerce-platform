using ACommerce.Kits.Versions.Templates;
using Ejar.Customer.UI;
using Ejar.Customer.UI.ClientHost;
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
.AddHttpMessageHandler<AppVersionHeadersHandler>()
.AddHttpMessageHandler<AuthHeadersHandler>();

// خدمات الـ UI القديمة (AppStore, OpEngine، dispatchers، chat client…) —
// تَبقى موجودة بالتوازي مع AddEjarCustomer() الجديد. التَنقية الكاملة في pass
// لاحق لمّا تُربَط الـ Ejar*Store stubs بكلّ الخدمات تماماً.
builder.Services.AddEjarCustomerUI();

// ClientHost الجديد — يُسجِّل صفحات الكيتس الـ 8 + IXxxStore bindings.
// الصفحات تُحَلّ runtime عبر KitPageRegistry بدون @page.
builder.Services.AddEjarCustomer();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(EjarCustomerUiExtensions).Assembly);

app.Run();
