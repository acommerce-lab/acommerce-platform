using ACommerce.Culture.Defaults;
using ACommerce.Kits.Versions.Templates;
using Ashare.V3.Customer.UI.ClientHost;
using Ejar.Customer.UI;
using App = Ashare.V3.Web.Components.App;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// نَفس HttpClient name "ejar" لأَنّ كُلّ القالَب + ApiClients يَستَدعونَه
// بِهذا الاسم. الاختِلاف فَقَط في BaseAddress — نَقرَأها مِن AshareApi:BaseUrl
// (افتِراضيّ: localhost:5400 — Ashare V3 backend).
var apiBase = builder.Configuration["AshareApi:BaseUrl"] ?? "http://localhost:5400";
var appVersion = builder.Configuration["App:Version"] ?? "1.0.0";
builder.Services.AddSingleton(new AppVersionInfo(Platform: "web", Version: appVersion));

builder.Services.AddHttpClient("ejar", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<CultureHeadersHandler>()
.AddHttpMessageHandler<AppVersionHeadersHandler>();

// نُقطة دَخول واحِدَة لِكامِل قالَب Customer.Marketplace + طَبَقَة Ashare V3
// (تَفوز عَلى translations Ejar V1 لِـ app.name + home.*).
builder.Services.AddAshareV3Customer();

// قَرار صَريح: V3 يَستَخدِم Nafath login UI بَدَل Phone-OTP الافتِراضي
// في Marketplace template. AddSingleton (لا TryAdd) يَتَجاوَز الافتِراضي.
builder.Services.AddSingleton<Ejar.Customer.UI.Components.Pages.Auth.IAuthLoginUi>(
    new Ejar.Customer.UI.Components.Pages.Auth.StaticAuthLoginUi(
        typeof(Ejar.Customer.UI.Components.Pages.Auth.NafathLoginContent)));

// V3 لا يَستَخدِم باقات اشتِراك — الدَفع بِالإعلان الواحِد ⇒ إخفاء
// زُرَّي "الباقات" + "اشتِراكي" مَن صَفحَة /me. عِند إعادَة Subscriptions
// kit لاحِقاً، احذِف هذا السَطر فَقَط.
builder.Services.AddSingleton(new Ejar.Customer.UI.Services.MarketplaceUiOptions
{
    ShowSubscriptionsMenu = false,
});

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Ejar.Customer.UI.Components.Routes).Assembly);
app.Run();
