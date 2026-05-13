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
    // EntityDiscoveryRegistry يَحتاج كلّ entity يَستهلكها CrudActionInterceptor
    // (مَسار /cities و /amenities و /categories مَثَلاً). assembly الـ
    // EjarDbContext يَحوي Listing/User/etc.، لكن DiscoveryRegion +
    // DiscoveryAmenity + DiscoveryCategory + SupportTicket في assemblies
    // أخرى. نُسَجّلها هنا.
    .RegisterEntities(
        typeof(EjarDbContext).Assembly,
        typeof(ACommerce.Kits.Discovery.Domain.DiscoveryRegion).Assembly,
        typeof(ACommerce.Kits.Support.Domain.SupportTicket).Assembly,
        typeof(ACommerce.Favorites.Operations.Entities.Favorite).Assembly)

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
        .AddFavorites<EjarFavoritesStore>()
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

// DynamicAttributes kit — مَصدَر القَوالِب الَّتي تَخدِم /profile/edit
// ولِسِمات الإعلانات. EjarAttributeTemplateSource يَبني القَوالِب مَن
// كود ثابِت (EjarAttributes.cs)؛ لا جَدول DB يَحتاج migration.
builder.Services.AddSingleton<ACommerce.Kits.DynamicAttributes.Backend.IAttributeTemplateSource,
                              Ejar.Api.Data.Templates.EjarAttributeTemplateSource>();
// MVC scan الافتِراضي = entry assembly فَقَط ⇒ نُلحِق Application Part
// لِالتِقاط DynamicAttributesController مَن كيت الخَلفِيَّة.
builder.Services.AddControllers()
    .AddApplicationPart(typeof(ACommerce.Kits.DynamicAttributes.Backend.DynamicAttributesController).Assembly);

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
