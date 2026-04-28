using Ejar.Customer.UI;
using Ejar.Customer.UI.Components.Layout;
using Ejar.Customer.UI.Interceptors;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// نقطة جذر المكوّنات. WASM لا يحتاج <Routes> إضافي — نُحمِّل MainLayout
// مع Router داخلي عبر <App> لا. الأبسط: نضع Routes مباشرةً.
builder.RootComponents.Add<Ejar.Customer.UI.Components.Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HTTP → Ejar.Api. BaseAddress يأتي من appsettings.json (مع override
// عبر appsettings.Development.json) — نفس آلية Ejar.Web بالضبط.
var apiBase = builder.Configuration["EjarApi:BaseUrl"]
              ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddHttpClient("ejar", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<CultureHeadersHandler>();

// HttpClient الافتراضي (للمكوّنات التي تحقن HttpClient بدون اسم)
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("ejar"));

builder.Services.AddEjarCustomerUI();

await builder.Build().RunAsync();
