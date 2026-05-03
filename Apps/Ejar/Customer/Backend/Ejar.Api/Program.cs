using ACommerce.Chat.Operations;
using ACommerce.Kits.Auth.Operations;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.ServiceHost;
using Ejar.Api.Data;
using Ejar.Api.Diagnostics;
using Ejar.Api.Interceptors;
using Ejar.Api.Middleware;
using Ejar.Api.Realtime;
using Ejar.Api.Stores;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// JWT متطابق بين AuthKit (يُصدِر التوكن) و JwtAuthModule (يتحقّق منه).
const string JwtSecret = "ejar_secret_key_12345678901234567890";

// EjarDatabase له منطق provider switching خاصّ — يُستدعى مباشرةً قبل
// UseDatabase الذي يضيف الـ glue (DbContext العام، UoW، Repositories).
builder.Services.AddEjarDatabase(builder.Configuration, builder.Environment);

builder.AddACommerceServiceHost(host => host
    // ── البنية التحتيّة العامّة ────────────────────────────────────────
    .UseSerilog("ejar")
    .UseDatabase<EjarDbContext>()
    .UseOperationEngine(typeof(Program).Assembly)
    .UseJwtAuthentication(jwt => { jwt.Secret = JwtSecret; jwt.Issuer = "ejar.api"; jwt.Audience = "ejar.mobile"; })
    .UseRealtime<EjarSignalRTransport, EjarRealtimeHub>()
    .UseControllers()
    .RegisterEntities(typeof(EjarDbContext).Assembly)

    // ── الكيتس ──────────────────────────────────────────────────────────
    .AddKits(kits => kits
        .AddAuth<EjarCustomerAuthUserStore>(
            new AuthKitJwtConfig(JwtSecret, "ejar.api", "ejar.mobile", "user", "User", AccessTokenLifetimeDays: 30),
            auth => auth.AddTwoFactor(tf => tf.UseMockSmsProvider()))
        .AddChat<EjarCustomerChatStore>()
        .AddChatPresenceProbe<EjarChatPresenceProbe>()
        .AddDiscovery()
        .AddSupport<EjarSupportStore>(opts =>
        {
            opts.PartyKind = "User";
            opts.AgentPoolDisplayName = "فريق دعم إيجار";
            var poolStr = builder.Configuration["Support:AgentPoolId"];
            if (Guid.TryParse(poolStr, out var poolGuid)) opts.AgentPoolPartyId = poolGuid;
        })
        .AddReports<EjarReportStore>(opts => opts.PartyKind = "User")
        .AddFavorites()
        .AddNotifications<EjarCustomerNotificationStore>(notif => notif
            .UseInAppProvider()
            .UseFirebaseProvider<EjarDeviceTokenStore>())
        .AddVersions<EjarVersionStore>()
        .AddSubscriptions<EjarSubscriptionStore, EjarPlanStore, EjarInvoiceStore>(
            opts => opts.OpenAccess = builder.Configuration.GetValue("Trial:OpenAccess", true))
        .AddListings<EjarListingStore>()
        .AddProfiles<EjarProfileStore>())

    // ── التراكيب ────────────────────────────────────────────────────────
    .AddCompositions(c => c
        .Add<ACommerce.Compositions.Support.SupportComposition>()
        .Add<ACommerce.Compositions.Chat.WithNotifications.ChatNotificationsComposition>()
        .Add<ACommerce.Compositions.Auth.WithSmsOtp.AuthSmsOtpComposition>()
        .Add<ACommerce.Compositions.Marketplace.MarketplaceComposition>())
);

// ── Ejar-specific extras ─────────────────────────────────────────────────
builder.Services.AddSingleton<IUserIdProvider, EjarUserIdProvider>();
builder.Services.AddSingleton<IOperationInterceptor, OperationLogInterceptor>();
builder.Services.AddScoped<ACommerce.Kits.Listings.Backend.IListingDetailEnricher, EjarListingDetailEnricher>();

var app = builder.Build();

// ─── DB Migrate + Seed + Versions promotion ─────────────────────────────
using (var scope = app.Services.CreateScope())
    await Ejar.Api.Bootstrap.EjarBootstrap.MigrateAndSeedAsync(scope.ServiceProvider, builder.Configuration);

// ─── Pipeline ────────────────────────────────────────────────────────────
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors(opt => opt
    .SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials());

app.UseACommerceServiceHost();        // Auth + Authorization + Controllers + Swagger

app.UseMiddleware<CurrentUserMiddleware>();
app.UseMiddleware<CurrentCultureMiddleware>();

app.MapHub<EjarRealtimeHub>("/realtime", options =>
    options.Transports = HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling);

app.Services.WireChatNotificationCoupling();

app.MapHealthEndpoints<EjarDbContext>("Ejar.Api");
app.MapEjarDiagnostics();

Log.Information("Ejar API ready [{Env}]", app.Environment.EnvironmentName);
app.Run();
