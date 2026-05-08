using ACommerce.Kits.Versions.Templates;
using Ejar.Customer.UI.ClientHost;
using Ejar.Customer.UI.Interceptors;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<Ejar.Customer.UI.Components.Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var appVersion = builder.Configuration["App:Version"] ?? "1.0.0";
builder.Services.AddSingleton(new AppVersionInfo(Platform: "wasm", Version: appVersion));

var apiBase = builder.Configuration["EjarApi:BaseUrl"] ?? builder.HostEnvironment.BaseAddress;
// Bearer header يَأتي مِن AuthenticatedHttpClient (ClientHost.Auth) — لا
// AuthHeadersHandler.
builder.Services.AddHttpClient("ejar", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<CultureHeadersHandler>()
.AddHttpMessageHandler<AppVersionHeadersHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("ejar"));

builder.Services.AddEjarCustomer();

var host = builder.Build();
await host.Services.GetRequiredService<Ejar.Customer.UI.Store.AppStorePersistence>().RestoreAsync();
await host.RunAsync();
