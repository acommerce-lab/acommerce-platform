using ACommerce.Chat.Operations;
using ACommerce.Kits.Auth.Operations;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.ServiceHost;
using Ashare.V3.Data;
using Ashare.V3.Api.Diagnostics;
using Ashare.V3.Api.Interceptors;
using Ashare.V3.Api.Middleware;
using Ashare.V3.Api.Realtime;
using Ashare.V3.Api.Stores;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// JWT متطابق بين AuthKit (يُصدِر التوكن) و JwtAuthModule (يتحقّق منه).
const string JwtSecret = "ashare_v3_secret_key_12345678901234567890";

// AshareV3Database له منطق provider switching خاصّ — يُستدعى مباشرةً قبل
// UseDatabase الذي يضيف الـ glue (DbContext العام، UoW، Repositories).
builder.Services.AddAshareV3Database(builder.Configuration, builder.Environment);

builder.AddACommerceServiceHost(host => host
    // ── البنية التحتيّة العامّة ────────────────────────────────────────
    .UseSerilog("ashare-v3")
    .UseDatabase<AshareV3DbContext>()
    .UseOperationEngine(typeof(Program).Assembly)
    .UseJwtAuthentication(jwt => { jwt.Secret = JwtSecret; jwt.Issuer = "ashare.v3.api"; jwt.Audience = "ashare.v3.mobile"; })
    .UseRealtime<AshareV3SignalRTransport, AshareV3RealtimeHub>()
    .UseControllers()
    // EntityDiscoveryRegistry يَحتاج كلّ entity يَستهلكها CrudActionInterceptor
    // (مَسار /cities و /amenities و /categories مَثَلاً). assembly الـ
    // AshareV3DbContext يَحوي Listing/User/etc.، لكن DiscoveryRegion +
    // DiscoveryAmenity + DiscoveryCategory + SupportTicket في assemblies
    // أخرى. نُسَجّلها هنا.
    .RegisterEntities(
        typeof(AshareV3DbContext).Assembly,
        typeof(ACommerce.Kits.Discovery.Domain.DiscoveryRegion).Assembly,
        typeof(ACommerce.Kits.Support.Domain.SupportTicket).Assembly,
        typeof(ACommerce.Favorites.Operations.Entities.Favorite).Assembly)

    // ── الكيتس ──────────────────────────────────────────────────────────
    .AddKits(kits => kits
        .AddAuth<AshareV3CustomerAuthUserStore>(
            new AuthKitJwtConfig(JwtSecret, "ashare.v3.api", "ashare.v3.mobile", "user", "User", AccessTokenLifetimeDays: 30),
            auth => auth.AddTwoFactor(tf => tf.UseMockSmsProvider()))
        .AddChat<AshareV3CustomerChatStore>()
        .AddChatPresenceProbe<AshareV3ChatPresenceProbe>()
        .AddDiscovery()
        .AddSupport<AshareV3SupportStore>(opts =>
        {
            opts.PartyKind = "User";
            opts.AgentPoolDisplayName = "فريق دعم عشير";
            var poolStr = builder.Configuration["Support:AgentPoolId"];
            if (Guid.TryParse(poolStr, out var poolGuid)) opts.AgentPoolPartyId = poolGuid;
        })
        .AddReports<AshareV3ReportStore>(opts => opts.PartyKind = "User")
        .AddFavorites<AshareV3FavoritesStore>()
        .AddNotifications<AshareV3CustomerNotificationStore>(notif => notif
            .UseInAppProvider()
            .UseFirebaseProvider<AshareV3DeviceTokenStore>())
        .AddVersions<AshareV3VersionStore>()
        .AddSubscriptions<AshareV3SubscriptionStore, AshareV3PlanStore, AshareV3InvoiceStore>(
            opts => opts.OpenAccess = builder.Configuration.GetValue("Trial:OpenAccess", true))
        .AddListings<AshareV3ListingStore>()
        .AddProfiles<AshareV3ProfileStore>())

    // ── التراكيب ────────────────────────────────────────────────────────
    .AddCompositions(c => c
        .Add<ACommerce.Compositions.Support.SupportComposition>()
        .Add<ACommerce.Compositions.Chat.WithNotifications.ChatNotificationsComposition>()
        .Add<ACommerce.Compositions.Auth.WithSmsOtp.AuthSmsOtpComposition>()
        .Add<ACommerce.Compositions.Marketplace.MarketplaceComposition>())
);

// ── AshareV3-specific extras ─────────────────────────────────────────────────
builder.Services.AddSingleton<IUserIdProvider, AshareV3UserIdProvider>();
builder.Services.AddSingleton<IOperationInterceptor, OperationLogInterceptor>();
builder.Services.AddScoped<ACommerce.Kits.Listings.Backend.IListingDetailEnricher, AshareV3ListingDetailEnricher>();

var app = builder.Build();

// ─── DB Migrate + Seed + Versions promotion ─────────────────────────────
using (var scope = app.Services.CreateScope())
    await Ashare.V3.Api.Bootstrap.AshareV3Bootstrap.MigrateAndSeedAsync(scope.ServiceProvider, builder.Configuration);

// ─── Pipeline ────────────────────────────────────────────────────────────
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors(opt => opt
    .SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials());

app.UseACommerceServiceHost();        // Auth + Authorization + Controllers + Swagger

app.UseMiddleware<CurrentUserMiddleware>();
app.UseMiddleware<CurrentCultureMiddleware>();

app.MapHub<AshareV3RealtimeHub>("/realtime", options =>
    options.Transports = HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling);

app.Services.WireChatNotificationCoupling();

app.MapHealthEndpoints<AshareV3DbContext>("Ashare.V3.Api");
app.MapAshareV3Diagnostics();

Log.Information("AshareV3 API ready [{Env}]", app.Environment.EnvironmentName);
app.Run();
