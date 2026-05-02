using ACommerce.Authentication.TwoFactor.Providers.Sms.Mock.Extensions;
using ACommerce.Compositions.Core;
using ACommerce.Favorites.Backend;
using ACommerce.Favorites.Operations.Entities;
using ACommerce.Kits.Auth;
using ACommerce.Kits.Auth.Backend;
using ACommerce.Kits.Auth.Operations;
using ACommerce.Kits.Auth.TwoFactor.AsAuth;
using ACommerce.Chat.Operations;
using ACommerce.Kits.Chat;
using ACommerce.Kits.Chat.Backend;
using ACommerce.Kits.Discovery.Backend;
using ACommerce.Kits.Discovery.Domain;
using ACommerce.Kits.Listings.Backend;
using ACommerce.Kits.Notifications.Backend;
using ACommerce.Kits.Profiles.Backend;
using ACommerce.Kits.Reports.Backend;
using ACommerce.Kits.Reports.Domain;
using ACommerce.Kits.Subscriptions.Backend;
using ACommerce.Kits.Support.Backend;
using ACommerce.Kits.Support.Domain;
using ACommerce.Kits.Versions.Backend;
using ACommerce.Notification.Providers.Firebase.Extensions;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.ServiceHost;
using ACommerce.SharedKernel.Domain.Entities;
using ACommerce.SharedKernel.Infrastructure.EFCore.Factories;
using ACommerce.SharedKernel.Repositories.Interfaces;
using Ejar.Api.Data;
using Ejar.Api.Interceptors;
using Ejar.Api.Middleware;
using Ejar.Api.Realtime;
using Ejar.Api.Stores;
using Ejar.Domain;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════
// ① ServiceHost — البنية التحتيّة العامّة (Serilog + OpEngine + JWT + Realtime
// + Controllers + Swagger). كلّ سطر هنا يستبدل ~٣٠ سطراً من الـ wiring
// المباشر — التفاصيل في libs/host/ACommerce.ServiceHost.
// ═══════════════════════════════════════════════════════════════════════
builder.AddACommerceServiceHost(host => host
    .UseSerilog("ejar")
    .UseOperationEngine(typeof(Program).Assembly)
    .UseJwtAuthentication(jwt =>
    {
        // نفس قيم AuthKitJwtConfig أدناه — ServiceHost يثبّت scheme،
        // AuthKit يصدر التوكن. لو اختلفت القيم: التوكن الصادر لا يُتحقَّق منه.
        jwt.Secret   = "ejar_secret_key_12345678901234567890";
        jwt.Issuer   = "ejar.api";
        jwt.Audience = "ejar.mobile";
        jwt.RealtimeHubPath = "/realtime";
    })
    .UseRealtime<EjarSignalRTransport, EjarRealtimeHub>()
    .UseControllers()
);

// ═══════════════════════════════════════════════════════════════════════
// ② البنية التحتيّة الخاصّة بإيجار: DbContext + UoW + Repositories +
// IUserIdProvider + interceptor مخصّص للسجلّ + custom middleware.
// ═══════════════════════════════════════════════════════════════════════
builder.Services.AddEjarDatabase(builder.Configuration, builder.Environment);
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<EjarDbContext>());
builder.Services.AddScoped<IUnitOfWork, ACommerce.SharedKernel.Infrastructure.EFCore.EfUnitOfWork>();
builder.Services.AddScoped<IRepositoryFactory, RepositoryFactory>();
builder.Services.AddSingleton<IUserIdProvider, EjarUserIdProvider>();
builder.Services.AddSingleton<IOperationInterceptor, OperationLogInterceptor>();

// ═══════════════════════════════════════════════════════════════════════
// ③ الكيتس — كلّ كيت يستلزم store يكتبه التطبيق على كيانات DB الخاصّة.
// الترتيب يهمّ في حالتَين:
//   • AddChatKit يُسجّل IChatStore Singleton؛ نُعيد كـ Scoped لأنّ
//     EjarCustomerChatStore يستهلك EjarDbContext (Scoped).
//   • AddSupportKit بعد AddChatKit لأنّ EjarSupportStore يحقن IChatStore.
// ═══════════════════════════════════════════════════════════════════════
builder.Services.AddAuthKit<EjarCustomerAuthUserStore>(new AuthKitJwtConfig(
    "ejar_secret_key_12345678901234567890",
    "ejar.api", "ejar.mobile", "user", "User", AccessTokenLifetimeDays: 30))
    .AddMockSmsTwoFactor()
    .AddTwoFactorAsAuth();

builder.Services.AddChatKit<EjarCustomerChatStore>();
Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions
    .RemoveAll<IChatStore>(builder.Services);
builder.Services.AddScoped<IChatStore, EjarCustomerChatStore>();
builder.Services.AddScoped<IPresenceProbe, EjarChatPresenceProbe>();

builder.Services.AddDiscoveryKit();
builder.Services.AddSupportKit<EjarSupportStore>(opts =>
{
    opts.PartyKind = "User";
    opts.AgentPoolDisplayName = "فريق دعم إيجار";
    var poolStr = builder.Configuration["Support:AgentPoolId"];
    if (Guid.TryParse(poolStr, out var poolGuid)) opts.AgentPoolPartyId = poolGuid;
});
builder.Services.AddReportsKit<EjarReportStore>(opts => opts.PartyKind = "User");
builder.Services.AddFavoritesKit();
builder.Services.AddNotificationsKit<EjarCustomerNotificationStore>();
builder.Services.AddVersionsKit<EjarVersionStore>();
builder.Services.AddSubscriptionsKit<EjarSubscriptionStore, EjarPlanStore, EjarInvoiceStore>(
    opts => opts.OpenAccess = builder.Configuration.GetValue("Trial:OpenAccess", true));
builder.Services.AddListingsKit<EjarListingStore>();
builder.Services.AddProfilesKit<EjarProfileStore>();

// ═══════════════════════════════════════════════════════════════════════
// ④ التراكيب — تجمع interceptors فوق kits. لكلّ تركيب RequiredKits يفحص
// حضور الـ stores قبل التشغيل.
// ═══════════════════════════════════════════════════════════════════════
builder.Services.AddComposition<ACommerce.Compositions.Support.SupportComposition>();
builder.Services.AddComposition<ACommerce.Compositions.Chat.WithNotifications.ChatNotificationsComposition>();
builder.Services.AddComposition<ACommerce.Compositions.Auth.WithSmsOtp.AuthSmsOtpComposition>();
builder.Services.AddComposition<ACommerce.Compositions.Marketplace.MarketplaceComposition>();

// ═══════════════════════════════════════════════════════════════════════
// ⑤ Firebase FCM — يُسجَّل فقط لو الـ creds موجودة. هذا الـ block يبقى في
// التطبيق لأنّه يحتاج resolution خاصّ لمسار credentials نسبيّ على IIS.
// ═══════════════════════════════════════════════════════════════════════
{
    var fbCfg = builder.Configuration.GetSection(
        ACommerce.Notification.Providers.Firebase.Options.FirebaseOptions.SectionName);
    var credPath = fbCfg["CredentialsFilePath"];
    if (!string.IsNullOrWhiteSpace(credPath) && !Path.IsPathRooted(credPath))
    {
        var abs = Path.Combine(builder.Environment.ContentRootPath, credPath);
        if (File.Exists(abs)) { fbCfg["CredentialsFilePath"] = abs; credPath = abs; }
        else                  { Log.Warning("Ejar.Firebase: {Path} not found", credPath); credPath = null; }
    }
    var hasCreds = !string.IsNullOrWhiteSpace(fbCfg["CredentialsJson"])
                || !string.IsNullOrWhiteSpace(credPath);
    if (hasCreds)
    {
        builder.Services.AddSingleton<
            ACommerce.Notification.Providers.Firebase.Storage.IDeviceTokenStore,
            EjarDeviceTokenStore>();
        builder.Services.AddFirebaseNotificationChannel(builder.Configuration);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// ⑥ Entity Discovery (للـ generic CRUD path).
// ═══════════════════════════════════════════════════════════════════════
EntityDiscoveryRegistry.RegisterEntity<UserEntity>();
EntityDiscoveryRegistry.RegisterEntity<ListingEntity>();
EntityDiscoveryRegistry.RegisterEntity<ConversationEntity>();
EntityDiscoveryRegistry.RegisterEntity<MessageEntity>();
EntityDiscoveryRegistry.RegisterEntity<NotificationEntity>();
EntityDiscoveryRegistry.RegisterEntity<PlanEntity>();
EntityDiscoveryRegistry.RegisterEntity<SubscriptionEntity>();
EntityDiscoveryRegistry.RegisterEntity<InvoiceEntity>();
EntityDiscoveryRegistry.RegisterEntity<DiscoveryCategory>();
EntityDiscoveryRegistry.RegisterEntity<DiscoveryRegion>();
EntityDiscoveryRegistry.RegisterEntity<DiscoveryAmenity>();
EntityDiscoveryRegistry.RegisterEntity<Favorite>();
EntityDiscoveryRegistry.RegisterEntity<SupportTicket>();
EntityDiscoveryRegistry.RegisterEntity<ReportEntity>();

var app = builder.Build();

// ═══════════════════════════════════════════════════════════════════════
// ⑦ DB Migrate + Seed + Versions promotion (StartupHook بدل scope manual).
// ═══════════════════════════════════════════════════════════════════════
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EjarDbContext>();

    var resetRequested = string.Equals(
        Environment.GetEnvironmentVariable("EJAR_DB_RESET"),
        "true", StringComparison.OrdinalIgnoreCase);
    if (resetRequested)
    {
        Log.Warning("Ejar.Db: EJAR_DB_RESET=true — dropping database");
        db.Database.EnsureDeleted();
    }

    var isSqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
    if (isSqlite) db.Database.EnsureCreated();
    else
    {
        var pending = db.Database.GetPendingMigrations().ToList();
        if (pending.Count > 0)
        {
            Log.Information("Ejar.Db: applying {N} migration(s)", pending.Count);
            db.Database.Migrate();
        }
        else DbInitializer.EnsureAppVersionsTable(db);
    }

    if (!db.Users.Any()) DbInitializer.Seed(db);
    DbInitializer.SeedAppVersionsIfMissing(db);

    try { await VersionsBootstrap.PromoteFromConfigAsync(scope.ServiceProvider, builder.Configuration); }
    catch (Exception ex) { Log.Warning(ex, "Ejar.Versions bootstrap failed"); }
}

// ═══════════════════════════════════════════════════════════════════════
// ⑧ Pipeline: middleware order. ServiceHost سجّل بعضه (Auth + Controllers
// + Swagger). البقيّة هنا بترتيب صريح: GlobalExceptionMiddleware أوّلاً،
// CORS مفتوح، ثمّ ServiceHost، ثمّ middleware إيجار المخصّص.
// ═══════════════════════════════════════════════════════════════════════
app.UseMiddleware<GlobalExceptionMiddleware>();

// CORS مفتوح تماماً (لا UseCors module: إيجار يحتاج SetIsOriginAllowed=true
// لـ SignalR + dev WASM على origins عشوائيّة).
app.UseCors(opt => opt
    .SetIsOriginAllowed(_ => true)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials());

// ServiceHost يطبّق UseAuthentication + UseAuthorization + MapControllers + Swagger.
// لكنّ middleware إيجار المخصّص يجب أن تأتي بين Authentication و Authorization
// — لذلك نستدعيها هنا (ServiceHost.UseAuthentication ثمّ هذا ثمّ Authorization).
// لذلك نستعمل النسخة الـ async لكي تنفّذ StartupHooks (لا hooks الآن).
app.UseACommerceServiceHost();

// CurrentUser/Culture middleware بين Auth و MapControllers — بعد UseACommerceServiceHost
// هذا قد لا يعمل دائماً بترتيب صحيح. للحفاظ على السلوك السابق نسجّلها قبل
// UseACommerceServiceHost إن لم تحوي MapControllers.
app.UseMiddleware<CurrentUserMiddleware>();
app.UseMiddleware<CurrentCultureMiddleware>();

// SignalR hub — مسار وقطعَتَي transport (SSE + LongPolling) خاصّتَين بـ
// runasp.net (لا WebSockets).
app.MapHub<EjarRealtimeHub>("/realtime", options =>
{
    options.Transports = HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;
});

// chat ↔ notif coupling — يربط interceptors التراكيب.
app.Services.WireChatNotificationCoupling();

// Health + diagnostic endpoints — Ejar-specific.
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
