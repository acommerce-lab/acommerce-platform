using ACommerce.Kit.Auth.Providers.MockNafath;
using ACommerce.Kit.Auth.Providers.MockSms;
using ACommerce.Kit.Auth.Server;
using ACommerce.Kit.Culture;
using ACommerce.Kit.Delivery;
using ACommerce.Kit.Files;
using ACommerce.Kit.Maps;
using ACommerce.Kit.Payments;
using ACommerce.Kit.Realtime.Server;
using ACommerce.Kit.Versions;
using ACommerce.Platform.Hosting;
using ACommerce.Templates.Customer.Marketplace;
using ACommerce.Templates.Customer.Marketplace.Components;
using ACommerce.V1.App.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformHost(host => host
    .AddKitAssembly(typeof(ACommerce.Kit.Tenants.Server.TenantHandlers).Assembly)
    .AddKitAssembly(typeof(ACommerce.Kit.Listings.Server.ListingHandlers).Assembly)
    .AddKitAssembly(typeof(AuthHandlers).Assembly)
    .AddKitAssembly(typeof(ACommerce.Kit.Notifications.Server.NotificationHandlers).Assembly)
    .AddKitAssembly(typeof(ACommerce.Kit.Chat.Server.ChatHandlers).Assembly)
    .AddKitAssembly(typeof(ACommerce.Kit.Favorites.Server.FavoriteHandlers).Assembly)
    .AddKitAssembly(typeof(ACommerce.Kit.Subscriptions.Server.SubscriptionHandlers).Assembly)
    .AddKitAssembly(typeof(ACommerce.Kit.Support.Server.TicketHandlers).Assembly)
    .AddKitAssembly(typeof(ACommerce.Kit.Profiles.Server.ProfileHandlers).Assembly)
    .AddKitAssembly(typeof(ACommerce.Kit.Cart.Server.CartHandlers).Assembly)
    .AddKitAssembly(typeof(ACommerce.Kit.Reports.Server.ReportHandlers).Assembly)
    .AddKitAssembly(typeof(RealtimeBroadcastHandler).Assembly));

// نَمَط ثَقافيّ + بَوّابَة إصدار (W3 — kits ناقِصَة مَنقولَة بِنَمَط v1).
builder.Services.AddCultureContext();
builder.Services.AddVersionGate(opts =>
{
    opts.MinimumSupported = "1.0.0";
    opts.LatestSuggested = "1.0.0";
});

// مُزَوِّدو الـ Auth (mock — استَبدِلهم بـ Twilio/Nafath فعليّ في الإنتاج)
builder.Services.AddMockSmsChannel();
builder.Services.AddMockNafathChannel(opts => { opts.DisplayCode = "00"; opts.AutoApproveSeconds = 5; });

// مُزَوِّدو البِنيَة (mock — استَبدِلهم لاحِقاً بِـ Moyasar/Saee/Google Maps).
builder.Services.AddMockMaps();
builder.Services.AddMockDelivery();
builder.Services.AddMockPayments();

// تَخزين مَلَفّات — Local (افتِراضيّ، صَفّ wwwroot/uploads). لِلإنتاج
// بَدِّل بِـ AddAliyunOssFileStorage(...) أو AddGoogleCloudFileStorage(...).
builder.Services.AddLocalFileStorage(opts =>
{
    opts.RootPath = Path.Combine(builder.Environment.WebRootPath ?? "wwwroot", "uploads");
    opts.PublicPathPrefix = "/uploads";
});

// القالَب — يُسَجِّل AuthSession + HttpContextAccessor
builder.Services.AddCustomerMarketplaceTemplate();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await PlatformSeed.RunAsync(scope.ServiceProvider);
    // اِربِط المَتاجِر القَديمَة بِأَوَّل مُستَخدِم studio (إن وُجِد).
    var docStore = scope.ServiceProvider.GetRequiredService<Marten.IDocumentStore>();
    await ACommerce.Templates.Customer.Marketplace.Services.Incubator
        .StudioOwnershipSeeder.RunAsync(docStore);

    // بَيانات اختِبار لِفَحص Layer 6 — لا تَعمَل في الإنتاج. تُفَعَّل
    // بِـ ENV TEST_DATA_SEED=1، وإلّا تُتَجاوَز.
    if (Environment.GetEnvironmentVariable("TEST_DATA_SEED") == "1")
        await TestDataSeeder.RunAsync(scope.ServiceProvider);
}

app.UsePlatformHost();

// تَفعيل خِدمَة المَلَفّات المَحَلِّيَّة (Local provider فَقَط — تُتَجاهَل لَو
// السيرفِر يَستَخدِم Aliyun/GCS مَع CDN).
if (app.Services.GetService<IFileStorage>() is LocalFileStorage)
    app.UseLocalFileStorage();

// W3 middleware — Culture + Version gate.
app.UseCultureContext();
app.UseVersionGate();

// القالَب — يُسَجِّل form endpoints (auth/login/logout/chat send/favorite/...)
app.MapCustomerMarketplaceTemplate();

app.MapHub<RealtimeHub>("/realtime");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
