using ACommerce.Chat.Operations;
using ACommerce.Kits.Auth.Operations;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.ServiceHost;
using Ejar.Api.Data;
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
    .UseFirebaseIfConfigured<EjarDeviceTokenStore>()
    .UseControllers()
    .RegisterEntities(typeof(EjarDbContext).Assembly)

    // ── الكيتس ──────────────────────────────────────────────────────────
    .AddKits(kits => kits
        .AddAuth<EjarCustomerAuthUserStore>(new AuthKitJwtConfig(
            JwtSecret, "ejar.api", "ejar.mobile", "user", "User", AccessTokenLifetimeDays: 30))
        .AddTwoFactorMockSms()
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
        .AddNotifications<EjarCustomerNotificationStore>()
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
{
    var db = scope.ServiceProvider.GetRequiredService<EjarDbContext>();

    if (string.Equals(Environment.GetEnvironmentVariable("EJAR_DB_RESET"), "true", StringComparison.OrdinalIgnoreCase))
    {
        Log.Warning("Ejar.Db: EJAR_DB_RESET=true — dropping database");
        db.Database.EnsureDeleted();
    }

    var isSqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
    if (isSqlite) db.Database.EnsureCreated();
    else
    {
        var pending = db.Database.GetPendingMigrations().ToList();
        if (pending.Count > 0) { Log.Information("Ejar.Db: applying {N} migration(s)", pending.Count); db.Database.Migrate(); }
        else                    DbInitializer.EnsureAppVersionsTable(db);
    }

    if (!db.Users.Any()) DbInitializer.Seed(db);
    DbInitializer.SeedAppVersionsIfMissing(db);

    try { await VersionsBootstrap.PromoteFromConfigAsync(scope.ServiceProvider, builder.Configuration); }
    catch (Exception ex) { Log.Warning(ex, "Ejar.Versions bootstrap failed"); }
}

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

// ─── Health + Diagnostic endpoints ───────────────────────────────────────
var healthHandler = (EjarDbContext db) =>
{
    var dbOk = false;
    try { dbOk = db.Database.CanConnect(); } catch { }
    return Results.Ok(new {
        status   = dbOk ? "healthy" : "degraded",
        db       = dbOk ? "ok" : "unreachable",
        time     = DateTime.UtcNow,
        service  = "Ejar.Api",
        provider = db.Database.ProviderName
    });
};
app.MapGet("/healthz", healthHandler).AllowAnonymous();
app.MapGet("/health",  healthHandler).AllowAnonymous();

app.MapGet("/diag/schema", async (EjarDbContext db) =>
{
    object Try(Func<object> f) { try { return f(); } catch (Exception ex) { return new { error = ex.GetType().Name, message = ex.Message }; } }
    var applied = new List<string>(); var pending = new List<string>();
    try
    {
        applied.AddRange(await db.Database.GetAppliedMigrationsAsync());
        pending.AddRange(await db.Database.GetPendingMigrationsAsync());
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, error = "migrations_history_unreadable", message = ex.Message });
    }
    return Results.Json(new {
        ok        = true,
        provider  = db.Database.ProviderName,
        canConnect = Try(() => (object)db.Database.CanConnect()),
        applied, pending,
        counts = new {
            users          = Try(() => (object)db.Users.Count()),
            listings       = Try(() => (object)db.Listings.Count()),
            conversations  = Try(() => (object)db.Conversations.Count()),
            favorites      = Try(() => (object)db.Favorites.Count()),
            plans          = Try(() => (object)db.Plans.Count()),
            subscriptions  = Try(() => (object)db.Subscriptions.Count()),
            appVersions    = Try(() => (object)db.AppVersions.Count())
        }
    });
}).AllowAnonymous();

Log.Information("Ejar API ready [{Env}]", app.Environment.EnvironmentName);
app.Run();
