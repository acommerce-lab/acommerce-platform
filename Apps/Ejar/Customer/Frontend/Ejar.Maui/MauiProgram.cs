using Ejar.Customer.UI.ClientHost;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Hosting;

namespace Ejar.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        using var stream = Task.Run(() => FileSystem.OpenAppPackageFileAsync("appsettings.json")).Result;
        var cfg = new ConfigurationBuilder().AddJsonStream(stream).Build();
        var apiBase = cfg["EjarApi:BaseUrl"] ?? "https://api.ejar.ye";

        builder.Services.AddHttpClient("ejar", c =>
        {
            c.BaseAddress = new Uri(apiBase);
            c.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddEjarCustomer();   // قالَب Customer.Marketplace + bindings
        return builder.Build();
    }
}
