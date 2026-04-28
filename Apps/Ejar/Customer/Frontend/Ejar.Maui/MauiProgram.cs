using Ejar.Customer.UI;
using Ejar.Customer.UI.Interceptors;
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
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                // إن أُضيفت خطوط مخصّصة في Resources/Fonts، تُسجَّل هنا.
            });

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // appsettings.json يُحمَّل من الـ MauiAsset (Resources/Raw/appsettings.json).
        // BaseUrl هنا يكون https://api.ejar.ye للإنتاج. أثناء التطوير على المحاكي:
        // Android emulator يصل للـ host عبر 10.0.2.2 → استخدم
        // appsettings.Development.json أو خصّص EJAR_API_BASE قبل الإقلاع.
        using var stream = Task.Run(() => FileSystem.OpenAppPackageFileAsync("appsettings.json")).Result;
        var cfg = new ConfigurationBuilder().AddJsonStream(stream).Build();
        var apiBase = cfg["EjarApi:BaseUrl"] ?? "https://api.ejar.ye";

        builder.Services.AddHttpClient("ejar", c =>
        {
            c.BaseAddress = new Uri(apiBase);
            c.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<CultureHeadersHandler>();

        builder.Services.AddScoped(sp =>
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("ejar"));

        builder.Services.AddEjarCustomerUI();

        return builder.Build();
    }
}
