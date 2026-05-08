using ACommerce.Kits.Versions.Templates;
using Ejar.Customer.UI.ClientHost;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// نُقطَة جَذر المُكَوِّنات — نَفس Routes الذي يَستَخدِمه Ejar.Web (server).
builder.RootComponents.Add<Ejar.Customer.UI.Components.Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var appVersion = builder.Configuration["App:Version"] ?? "1.0.0";
builder.Services.AddSingleton(new AppVersionInfo(Platform: "wasm", Version: appVersion));

var apiBase = builder.Configuration["EjarApi:BaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddHttpClient("ejar", c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddEjarCustomer();

await builder.Build().RunAsync();
