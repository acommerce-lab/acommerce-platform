using System.Reflection;
using System.Text;
using ACommerce.Kits.Auth;
using ACommerce.Kits.Auth.Operations.Extensions;
using ACommerce.Kits.Auth.TwoFactor.AsAuth;
using ACommerce.Authentication.TwoFactor.Providers.Sms.Mock.Extensions;
using ACommerce.Subscriptions.Operations.Extensions;
using ACommerce.Kits.Chat;
using ACommerce.Chat.Operations;
using ACommerce.Kits.Discovery.Backend;
using ACommerce.Realtime.Providers.InMemory.Extensions;
using ACommerce.Realtime.Providers.SignalR.Extensions;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Extensions;
using ACommerce.OperationEngine.Interceptors.Extensions;
using ACommerce.OperationEngine.Wire.Http;
using ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;
using ACommerce.SharedKernel.Repositories.Interfaces;
using ACommerce.SharedKernel.Domain.Entities;
using ACommerce.Kits.Support.Domain;
using ACommerce.Kits.Reports.Backend;
using ACommerce.Kits.Reports.Domain;
using ACommerce.Compositions.Core;
using ACommerce.Kits.Discovery.Domain;
using ACommerce.Favorites.Operations.Entities;
using Ejar.Api.Data;
using Ejar.Api.Interceptors;
using Ejar.Api.Middleware;
using Ejar.Api.Realtime;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ACommerce.Kits.Support.Backend;
using ACommerce.Favorites.Backend;
using ACommerce.OperationEngine.DataInterceptors;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.Realtime.Providers.SignalR;
using Ejar.Domain;
using Ejar.Api.Stores;
using ACommerce.Kits.Auth.Backend;
using ACommerce.Kits.Chat.Backend;
using ACommerce.Kits.Notifications.Backend;
using ACommerce.Kits.Versions.Backend;
using ACommerce.Notification.Providers.Firebase.Extensions;
using ACommerce.Kits.Auth.Operations;
using ACommerce.SharedKernel.Infrastructure.EFCores.Context;
using ACommerce.SharedKernel.Infrastructure.EFCore.Factories;

var builder = WebApplication.CreateBuilder(args);

// 1. Logging (Serilog)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/ejar-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Database — provider يُختار حسب Database:Provider (sqlite/mssql)
// والـ connection string من Database:ConnectionString. الـ extension يحلّ
// مسار SQLite الافتراضي إلى <repo>/data أو ContentRoot عبر PlatformDataRoot.
builder.Services.AddEjarDatabase(builder.Configuration, builder.Environment);
// تسجيله كـ DbContext عام للمستودعات
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<EjarDbContext>());
builder.Services.AddScoped<IRepositoryFactory, RepositoryFactory>();

// 3. Operation Engine & Interceptors
builder.Services.AddOperationEngine();
builder.Services.AddOperationInterceptors(registry => {
    registry.Register(new CrudActionInterceptor());
});
builder.Services.AddSingleton<IOperationInterceptor, OperationLogInterceptor>();

// Cross-cutting gates — كلّها معترضات Pre تتجاوز عمليّاتها الذاتيّة.
// Versions يُسجَّل من AddVersionsKit أدناه. Auth يُسجَّل هنا.
// Subscriptions interceptor (لـ requires_subscription) متاح ولكن لم يُفعَّل
// في Ejar حالياً لأنّه لم يُسجَّل ISubscriptionProvider — يعمل تلقائياً
// لو أُضيف لاحقاً (المعترض يمرّ بسلام لو لم يُحقن المزوّد).
builder.Services.AddAuthGateInterceptor();
builder.Services.AddSubscriptionGateInterceptor();

// 4. MediatR
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
});

// 5. Kits Registration
builder.Services.AddAuthKit<EjarCustomerAuthUserStore>(new AuthKitJwtConfig(
    "ejar_secret_key_12345678901234567890",
    "ejar.api",
    "ejar.mobile",
    "user",
    "User",
    30
))
    .AddMockSmsTwoFactor()
    .AddTwoFactorAsAuth();

// JWT Bearer scheme — لازم لتفعيل [Authorize] على المتحكّمات. AuthKit يُصدر
// التوكن لكنّه لا يُسجّل scheme التحقّق؛ بدون هذا كلّ مسار [Authorize] يُلقي
// "No authentication scheme was configured" → GlobalExceptionMiddleware يحوّله
// إلى 500 (لاحظنا في الـ console: /me/profile, /my-listings, /favorites,
// /conversations/start, /me/subscription كلّها 500). نستخدم نفس السرّ/المُصدِر
// /الجمهور من AuthKitJwtConfig أعلاه ليتطابقا. MapInboundClaims=false يُبقي
// "user_id" كما هو بدل ترجمته إلى ClaimTypes.NameIdentifier فيستهلكه
// CurrentUserGuid مباشرةً.
const string ejarJwtSecret = "ejar_secret_key_12345678901234567890";
const string ejarJwtIssuer = "ejar.api";
const string ejarJwtAudience = "ejar.mobile";
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                                          Encoding.UTF8.GetBytes(ejarJwtSecret)),
            ValidateIssuer           = true,
            ValidIssuer              = ejarJwtIssuer,
            ValidateAudience         = true,
            ValidAudience            = ejarJwtAudience,
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.FromMinutes(2),
        };
        // SignalR WebSocket لا يستطيع إرسال Authorization header، يمرّر التوكن
        // كـ ?access_token=… عند الاتصال بـ /realtime.
        o.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.Request.Path.StartsWithSegments("/realtime"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// SignalR transport — يستعمل EjarSignalRTransport (IHubContext<EjarRealtimeHub>)
// بدل SignalRRealtimeTransport في الكيت الذي يربط ثابتاً بـ AShareHub.
// SignalR يفرز groups لكلّ Hub type على حدة، فلو استعملنا الكيت كما هو
// يُضاف connId إلى group على AShareHub بينما الاتصال على EjarRealtimeHub —
// الـ broadcast يبثّ إلى group فارغة ولا يصل أحد. هذا هو السبب الجذريّ
// لـ "كلّ شيء حيّ ما عدا التسليم".
//
// AddSignalR + AddRealtimeChannels نضيفهما يدوياً (بدل AddSignalRRealtimeTransport
// الذي يسجّل الـ transport الخاطئ).
builder.Services.AddSignalR();
builder.Services.AddSingleton<ACommerce.Realtime.Operations.Abstractions.IRealtimeTransport, EjarSignalRTransport>();
ACommerce.Realtime.Operations.RealtimeExtensions.AddRealtimeChannels(builder.Services);

builder.Services.AddSingleton<ACommerce.Realtime.Providers.InMemory.InMemoryConnectionTracker>();
builder.Services.AddSingleton<ACommerce.Realtime.Operations.Abstractions.IConnectionTracker>(
    sp => sp.GetRequiredService<ACommerce.Realtime.Providers.InMemory.InMemoryConnectionTracker>());
// IUserIdProvider مخصّص لقراءة "user_id" من JWT (MapInboundClaims=false
// يُلغي تحويل sub→NameIdentifier الافتراضيّ). بدونه IConnectionTracker
// لا يربط user→connection، فأيّ SendToUserAsync أو SendToGroupAsync لا
// يصل أحداً → كلّ realtime "زينة".
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, EjarUserIdProvider>();
builder.Services.AddChatKit<EjarCustomerChatStore>();
// AddChatKit يسجّل IChatStore كـ Singleton، لكن EjarCustomerChatStore يستهلك
// EjarDbContext (Scoped) → ValidateOnBuild يفشل بـ "Cannot consume scoped
// from singleton". نُزيل تسجيل الـ Singleton ثمّ نُسجّل Scoped.
Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions
    .RemoveAll<ACommerce.Kits.Chat.Backend.IChatStore>(builder.Services);
builder.Services.AddScoped<ACommerce.Kits.Chat.Backend.IChatStore, EjarCustomerChatStore>();

builder.Services.AddDiscoveryKit();
// Support kit بعد Chat kit لأنّ EjarSupportStore يحقن IChatStore.
// الـ AgentPoolPartyId ثابت قابل للضبط في appsettings (Support:AgentPoolId).
builder.Services.AddSupportKit<EjarSupportStore>(opts =>
{
    opts.PartyKind = "User";
    opts.AgentPoolDisplayName = "فريق دعم إيجار";
    var poolStr = builder.Configuration["Support:AgentPoolId"];
    if (Guid.TryParse(poolStr, out var poolGuid)) opts.AgentPoolPartyId = poolGuid;
});
// Reports kit: بلاغات "أبلِغ-وانسَ" بدون ردود — راجع
// libs/kits/Reports/ACommerce.Kits.Reports.Backend/ReportsController.cs.
builder.Services.AddReportsKit<EjarReportStore>(opts => opts.PartyKind = "User");

// ─── التراكيب الخارجيّة (compositions) ─────────────────────────────
// ChatRealtimeComposition يحقن interceptor على message.send يبثّ user-
// pinned للمرسل والمستلم عبر IRealtimeTransport. Chat kit يبقى نقيّاً
// (لا يعرف Realtime). راجع docs/COMPOSITION-MODEL.md.
builder.Services.AddComposition<ACommerce.Compositions.Chat.Realtime.ChatRealtimeComposition>();
// AuthSmsOtpComposition يفرض وجود IAuthUserStore + ITwoFactorChannel ويُلصِق
// تدقيقاً على auth.signin (سطر log منظَّم لكلّ محاولة). أيّ سلوك lattice
// آخر (rate-limit، إخطار إداريّ) يُضاف bundles داخل التركيب نفسه.
builder.Services.AddComposition<ACommerce.Compositions.Auth.WithSmsOtp.AuthSmsOtpComposition>();

builder.Services.AddFavoritesKit();
builder.Services.AddNotificationsKit<EjarCustomerNotificationStore>();
builder.Services.AddVersionsKit<EjarVersionStore>();

// Firebase Cloud Messaging — تسليم إشعارات للأجهزة (web push + Android/iOS)
// عبر FCM Admin SDK. ينعم بحياة لمّا الـ tab في الخلفيّة أو الجوّال مقفول
// (SignalR لا يُسلّم في تلك الحالات). شغّال فقط لو الـ config يحوي ServiceAccount
// JSON — وإلّا نتخطّى ولا نفجّر startup.
//
// ServiceAccount يُحضَر من: Firebase Console → Project Settings → Service
// accounts → Generate new private key. يُحفَظ كـ ملف على الخادم ويُشار إليه
// بـ Notifications:Firebase:CredentialsFilePath، أو يُلصَق محتواه في
// Notifications:Firebase:CredentialsJson (مفيد لـ secrets vault / env var).
{
    var fbCfg = builder.Configuration.GetSection(
        ACommerce.Notification.Providers.Firebase.Options.FirebaseOptions.SectionName);
    var credPath = fbCfg["CredentialsFilePath"];
    // حلّ المسار النسبيّ على ContentRoot — على IIS/runasp الـ CWD يكون
    // C:\windows\system32 لا مجلّد التطبيق، فـ GoogleCredential.FromFile
    // يفشل بـ FileNotFound لو تركناه نسبياً. ملف الاعتماد يُنشَر مع الـ
    // assemblies تحت ContentRootPath/Secrets/firebase-service-account.json.
    if (!string.IsNullOrWhiteSpace(credPath) && !Path.IsPathRooted(credPath))
    {
        var abs = Path.Combine(builder.Environment.ContentRootPath, credPath);
        if (File.Exists(abs))
        {
            fbCfg["CredentialsFilePath"] = abs;
            credPath = abs;
        }
        else
        {
            Log.Warning("Ejar.Firebase: CredentialsFilePath {Path} not found relative to {Root}", credPath, builder.Environment.ContentRootPath);
            credPath = null;
        }
    }

    var hasCreds = !string.IsNullOrWhiteSpace(fbCfg["CredentialsJson"])
                || !string.IsNullOrWhiteSpace(credPath);
    if (hasCreds)
    {
        // مخزن الرموز DB-backed قبل تسجيل الكيت — TryAdd داخل الكيت يحترم التسجيل
        // الموجود فلا يستبدله بـ InMemoryDeviceTokenStore.
        builder.Services.AddSingleton<
            ACommerce.Notification.Providers.Firebase.Storage.IDeviceTokenStore,
            EjarDeviceTokenStore>();
        builder.Services.AddFirebaseNotificationChannel(builder.Configuration);
        Log.Information("Ejar.Firebase: registered FCM channel + EjarDeviceTokenStore (creds={Source})",
            !string.IsNullOrWhiteSpace(fbCfg["CredentialsJson"]) ? "Json" : credPath);
    }
    else
    {
        Log.Information("Ejar.Firebase: skipped — set Notifications:Firebase:CredentialsJson or :CredentialsFilePath to enable");
    }
}

// 6. Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 7. Entity Discovery Registration
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

// 8. DB Schema Ensure & Seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EjarDbContext>();

    // مفتاح إعادة تهيئة لمرّة واحدة: اضبط متغيّر البيئة EJAR_DB_RESET=true قبل
    // النشر لإسقاط قاعدة البيانات بالكامل وإعادة بنائها من migrations + seed.
    // مفيد للقواعد القديمة المُنشأة بـ EnsureCreated() التي لا تستطيع Migrate()
    // إصلاحها. بعد نجاح النشرة الأولى، أزل المتغيّر فوراً وإلّا تُمسح البيانات
    // في كلّ بدء تشغيل.
    var resetRequested = string.Equals(
        Environment.GetEnvironmentVariable("EJAR_DB_RESET"),
        "true", StringComparison.OrdinalIgnoreCase);
    if (resetRequested)
    {
        Log.Warning("Ejar.Db: EJAR_DB_RESET=true — dropping database before migrate");
        db.Database.EnsureDeleted();
    }

    // نستخدم Migrate() بدل EnsureCreated() ليصبح schema drift قابلاً للإدارة.
    // استثناء: SQLite ليس له migrations في هذا المشروع (الـ migrations
    // مكتوبة لـ SQL Server: nvarchar(max), uniqueidentifier...). للـ tests
    // و dev المحلّيّ على SQLite نقع على EnsureCreated الذي يُولّد schema
    // متوافقاً تلقائياً من النموذج.
    var isSqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
    if (isSqlite)
    {
        db.Database.EnsureCreated();
    }
    else
    {
        var pending = db.Database.GetPendingMigrations().ToList();
        if (pending.Count > 0)
        {
            Log.Information("Ejar.Db: applying {Count} migration(s): {Names}",
                pending.Count, string.Join(", ", pending));
            db.Database.Migrate();
        }
        else
        {
            // قاعدة بيانات تُحسَب up-to-date. لو أُنشئت يدوياً أو بـ EnsureCreated
            // قد ينقصها جدول AppVersions الذي لم يُسجَّل في __EFMigrationsHistory.
            DbInitializer.EnsureAppVersionsTable(db);
        }
    }

    // البذور: تعمل عند كلّ بدء تشغيل، تتفقّد بنفسها قبل الإدراج.
    if (!db.Users.Any())
    {
        Log.Information("Ejar.Db: seeding initial data");
        DbInitializer.Seed(db);
        Log.Information("Ejar.Db: seeding complete");
    }
    DbInitializer.SeedAppVersionsIfMissing(db);

    // Promote-from-config: يقرأ Versions:Latest:{platform} من appsettings ويُسجّل
    // كلّ منها كـ Latest عبر IVersionStore. EjarVersionStore.UpsertAsync يُخفّض
    // أيّ Latest سابق في نفس المنصّة إلى Active تلقائياً، فالنشر يكفي وحده
    // ليصبح الإصدار الجديد هو الـ Latest بلا أيّ تدخّل إداريّ يدويّ.
    // try/catch لئلّا يمنع فشل الـ bootstrap (DB غير متاح، إلخ.) بقيّة التشغيل
    // — نسجّل ونمضي.
    try { await VersionsBootstrap.PromoteFromConfigAsync(scope.ServiceProvider, builder.Configuration); }
    catch (Exception ex) { Log.Warning(ex, "Ejar.Versions: bootstrap from config failed"); }
}

// 9. Middleware Pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS — لا بدّ أن نسمح بـ AllowCredentials لكي ينجح SignalR negotiate
// CORS — مفتوح بالكامل + AllowCredentials. AllowAnyOrigin مع AllowCredentials
// ممنوع من الـ spec فنستعمل SetIsOriginAllowed(_ => true) (نفس الأثر).
// فتح كامل ضروريّ لـ:
//   1. SignalR negotiate من أيّ origin (الواجهة المنشورة + dev محلّيّ + الجوّال)
//   2. WASM محلّيّ يستهلك API منشور أثناء التطوير
// المنطق المعقَّد السابق (Cors:AllowedOrigins + Patterns) كان يرفض localhost
// فيظنّ المستخدم أنّ الـ Backend مُعطَّل بينما الـ gateway يردّ 504 سريعاً
// لأنّه لا يجد Allow-Origin مطابق. التشديد يأتي لاحقاً عند الإطلاق العامّ
// عبر إضافة بوّابة API key أو CSRF token مستقلّ.
app.UseCors(opt => opt
    .SetIsOriginAllowed(_ => true)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials());

app.UseAuthentication();
app.UseMiddleware<CurrentUserMiddleware>();
app.UseMiddleware<CurrentCultureMiddleware>();
app.UseAuthorization();

app.MapControllers();
// EjarRealtimeHub يُضيف اشتراك المستخدم بـ notif:conv:X لكلّ محادثة عند
// كلّ اتصال SignalR جديد. AShareHub الخامّ لا يفعل ذلك، فالطرف الآخر
// لا يصله شيء حتى يفتح ChatRoom على المحادثة المحدّدة.
//
// runasp.net (IIS مشترك) لا يُفعّل WebSocket module افتراضياً، فلو أعلن
// الخادم WS متاحاً، الـ JS client يحاول WS أوّلاً ويفشل بدون fallback
// تلقائيّ إلى LongPolling. الحلّ: إعلان ServerSentEvents + LongPolling
// فقط — كلاهما HTTP عاديّ يعمل على أيّ مستضيف. WS ممكن إعادته لاحقاً
// لو نقلنا لـ Linux/Kestrel أو فعّلناه على IIS عبر web.config.
app.MapHub<EjarRealtimeHub>("/realtime", options =>
{
    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.ServerSentEvents
                       | Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
});

// قاعدة الـ chat ↔ notif coupling: عند فتح chat:conv:X يُقفَل notif:conv:X
// (لئلّا تتكرّر الإشعارات + الرسالة الحيّة)، وعند الإغلاق يُعاد فتحه.
app.Services.WireChatNotificationCoupling();

// نقطة الفحص الحيّ التي يستدعيها سكربت api-diagnostics.js في الواجهات
// (Web/WASM/MAUI) عند بدء كلّ جلسة لاختبار الوصول إلى الخدمة وقاعدة البيانات.
// نُعرّفها بمسارَين: /healthz هو القياسيّ في k8s/Azure، /health للتوافق
// الخلفيّ مع أيّ عميل قديم استخدم الاسم بلا الـ z.
var healthHandler = (EjarDbContext db) =>
{
    var dbOk = false;
    try { dbOk = db.Database.CanConnect(); }
    catch { /* dbOk = false */ }
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

// Diagnostic endpoint — يُظهر حالة الـ schema الفعليّة على الإنتاج. مفيد عندما
// تعود كلّ مسارات [Authorize] بـ 500: نفحص هل الجداول موجودة، هل الأعمدة الجديدة
// (ConversationEntity.OwnerId مثلاً) مطبَّقة، وأيّ migrations ناقصة. لا يكشف
// أيّ بيانات حسّاسة — فقط أسماء/أعمدة/migrations.
app.MapGet("/diag/schema", async (EjarDbContext db) =>
{
    object Try(Func<object> f) { try { return f(); } catch (Exception ex) { return new { error = ex.GetType().Name, message = ex.Message }; } }

    var applied = new List<string>();
    var pending = new List<string>();
    try
    {
        applied.AddRange(await db.Database.GetAppliedMigrationsAsync());
        pending.AddRange(await db.Database.GetPendingMigrationsAsync());
    }
    catch (Exception ex)
    {
        return Results.Json(new {
            ok = false,
            error = "migrations_history_unreadable",
            message = ex.Message,
            hint = "DB may have been created with EnsureCreated() — set EJAR_DB_RESET=true once and restart to wipe & re-migrate."
        });
    }

    return Results.Json(new {
        ok        = true,
        provider  = db.Database.ProviderName,
        canConnect = Try(() => (object)db.Database.CanConnect()),
        applied,
        pending,
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
