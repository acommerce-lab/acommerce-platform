using ACommerce.Authentication.Operations;
using ACommerce.Authentication.Operations.Abstractions;
using ACommerce.Authentication.Providers.Token;
using ACommerce.Authentication.Providers.Token.Extensions;
using ACommerce.Authentication.TwoFactor.Operations;
using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.Authentication.TwoFactor.Providers.Email.Extensions;
using ACommerce.Authentication.TwoFactor.Providers.Nafath.Extensions;
using ACommerce.Authentication.TwoFactor.Providers.Sms.Extensions;
using ACommerce.Favorites.Operations.Extensions;
using ACommerce.Files.Abstractions.Providers;
using ACommerce.Files.Operations.Extensions;
using ACommerce.Files.Storage.AliyunOSS.Extensions;
using ACommerce.Files.Storage.Local.Extensions;
using ACommerce.Notification.Operations.Abstractions;
using ACommerce.OperationEngine.Interceptors.Extensions;
using ACommerce.Notification.Providers.Firebase.Extensions;
using ACommerce.Notification.Providers.InApp.Extensions;
using ACommerce.OperationEngine.Core;
using ACommerce.Payments.Operations;
using ACommerce.Payments.Operations.Abstractions;
using ACommerce.Payments.Providers.Noon;
using ACommerce.Payments.Providers.Noon.Options;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.Realtime.Providers.InMemory.Extensions;
using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Abstractions.Repositories;
using ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;
using ACommerce.Translations.Operations.Extensions;
using Ashare.Api.Configuration;
using Ashare.Api.Entities;
using Ashare.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────
// Configuration: استبدال ${VAR} من متغيرات البيئة
// ─────────────────────────────────────────────────────────
EnvironmentVariableSubstitutionSource.ApplyToConfiguration((IConfigurationRoot)builder.Configuration);

// ─────────────────────────────────────────────────────────
// Serilog
// ─────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// ─────────────────────────────────────────────────────────
// Register Ashare.Api entities
// ─────────────────────────────────────────────────────────
EntityDiscoveryRegistry.RegisterEntity(typeof(User));
EntityDiscoveryRegistry.RegisterEntity(typeof(Category));
EntityDiscoveryRegistry.RegisterEntity(typeof(Listing));
EntityDiscoveryRegistry.RegisterEntity(typeof(Booking));
EntityDiscoveryRegistry.RegisterEntity(typeof(Ashare.Api.Entities.Payment));
EntityDiscoveryRegistry.RegisterEntity(typeof(Ashare.Api.Entities.Notification));
EntityDiscoveryRegistry.RegisterEntity(typeof(Conversation));
EntityDiscoveryRegistry.RegisterEntity(typeof(Message));
EntityDiscoveryRegistry.RegisterEntity(typeof(DeviceToken));
EntityDiscoveryRegistry.RegisterEntity(typeof(TwoFactorChallengeRecord));
EntityDiscoveryRegistry.RegisterEntity(typeof(Profile));
EntityDiscoveryRegistry.RegisterEntity(typeof(MediaFile));

// ─────────────────────────────────────────────────────────
// Database: SqlServer في الإنتاج، InMemory في التطوير
// ─────────────────────────────────────────────────────────
var dbProvider = builder.Configuration["Database:Provider"] ?? "InMemory";
var dbConnection = builder.Configuration["Database:ConnectionString"];

switch (dbProvider.ToLowerInvariant())
{
    case "sqlserver":
        if (string.IsNullOrWhiteSpace(dbConnection) || dbConnection.Contains("${"))
        {
            Log.Warning("Database:ConnectionString غير مُعيّن في env vars - الرجوع لـ InMemory");
            builder.Services.AddACommerceInMemoryDatabase("AsharePlatformDb");
        }
        else
        {
            Log.Information("Using SQL Server database");
            builder.Services.AddACommerceSqlServer(dbConnection);
        }
        break;

    case "sqlite":
        // See PlatformDataRoot.Resolve — walks up to the repo root so both
        // Ashare backends hit the same physical file.
        var ashareDbConn = !string.IsNullOrWhiteSpace(dbConnection)
            ? dbConnection
            : ACommerce.SharedKernel.Infrastructure.EFCores.PlatformDataRoot
                .SqliteConnectionString(builder.Environment.ContentRootPath, "ashare-platform.db");
        builder.Services.AddACommerceSQLite(ashareDbConn);
        break;

    default:
        Log.Information("Using InMemory database");
        builder.Services.AddACommerceInMemoryDatabase("AsharePlatformDb");
        break;
}

// ─────────────────────────────────────────────────────────
// MVC + Swagger + CORS
// ─────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// ─────────────────────────────────────────────────────────
// Core: OperationEngine
// ─────────────────────────────────────────────────────────
// OpEngine يجب أن يكون Scoped حتى يلتقط IServiceProvider الخاص بالطلب،
// مما يسمح للمعترضات الـ Scoped (مثل QuotaInterceptor) بأن تُحلّ من DbContext الحالي.
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// ─────────────────────────────────────────────────────────
// Realtime + Notifications
// ─────────────────────────────────────────────────────────
builder.Services.AddInMemoryRealtimeTransport();

builder.Services.AddInAppNotificationChannel(opt =>
{
    opt.MethodName = "ReceiveNotification";
    opt.AllowOffline = true;
});

// Firebase: نسجّل فقط إذا كانت بيانات الاعتماد موجودة
var firebaseCredsJson = builder.Configuration["Notifications:Firebase:CredentialsJson"];
var firebaseCredsFile = builder.Configuration["Notifications:Firebase:CredentialsFilePath"];
var hasFirebaseCreds = !string.IsNullOrWhiteSpace(firebaseCredsJson) && !firebaseCredsJson.Contains("${")
                    || !string.IsNullOrWhiteSpace(firebaseCredsFile) && !firebaseCredsFile.Contains("${");

if (hasFirebaseCreds)
{
    Log.Information("Firebase notification channel enabled");
    builder.Services.AddFirebaseNotificationChannel(builder.Configuration);
}
else
{
    Log.Warning("Firebase credentials not configured - Firebase channel disabled");
}

// ─────────────────────────────────────────────────────────
// File Storage: حسب الإعدادات
// ─────────────────────────────────────────────────────────
var storageProvider = builder.Configuration["Files:Storage:Provider"] ?? "Local";
switch (storageProvider.ToLowerInvariant())
{
    case "aliyunoss":
        var aliyunKey = builder.Configuration["Files:Storage:AliyunOSS:AccessKeyId"];
        if (!string.IsNullOrWhiteSpace(aliyunKey) && !aliyunKey.Contains("${"))
        {
            Log.Information("Using Aliyun OSS storage");
            builder.Services.AddAliyunOSSFileStorage(builder.Configuration);
        }
        else
        {
            Log.Warning("Aliyun credentials missing - falling back to no storage");
        }
        break;

    case "googlecloud":
        Log.Information("Using Google Cloud Storage");
        // builder.Services.AddGoogleCloudFileStorage(builder.Configuration);
        // (يحتاج Wiring مماثل)
        break;

    case "local":
        Log.Information("Using Local file storage");
        builder.Services.AddLocalFileStorage(builder.Configuration);
        break;

    default:
        Log.Information("No storage provider configured (storage operations disabled)");
        break;
}

// إضافة FileService فوق IStorageProvider (يرفع الخطأ إن لم يكن مُسجّلاً عند الاستخدام)
builder.Services.AddFileOperations();

// ─────────────────────────────────────────────────────────
// Authentication: JWT حقيقي (مطابق لما تفعله Ashare القديمة)
// ─────────────────────────────────────────────────────────
var jwtOptions = new JwtOptions
{
    Issuer = builder.Configuration["JWT:Issuer"] ?? "https://ashare.app",
    Audience = builder.Configuration["JWT:Audience"] ?? "ashare-api",
    SecretKey = builder.Configuration["JWT:SecretKey"]
        ?? "ChangeThisInProduction-secret-key-32-chars-min!!",
    AccessTokenLifetime = TimeSpan.TryParse(
        builder.Configuration["JWT:AccessTokenLifetime"], out var lt) ? lt : TimeSpan.FromDays(30)
};
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtTokenStore>();
builder.Services.AddSingleton<ITokenValidator>(sp => sp.GetRequiredService<JwtTokenStore>());
builder.Services.AddSingleton<ITokenIssuer>(sp => sp.GetRequiredService<JwtTokenStore>());
builder.Services.AddTokenAuthenticator();

// ASP.NET Core JWT Bearer لحماية النقاط الطرفية بـ [Authorize]
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<AuthConfig>(sp =>
{
    var c = new AuthConfig();
    c.AddAuthenticator(sp.GetRequiredService<TokenAuthenticator>());
    c.UseIssuer(sp.GetRequiredService<ITokenIssuer>());
    return c;
});
builder.Services.AddScoped<AuthService>();

// ─────────────────────────────────────────────────────────
// 2FA: SMS + Email + Nafath (Nafath إذا كانت بيانات الاعتماد موجودة)
// ─────────────────────────────────────────────────────────
builder.Services.AddSmsTwoFactor();
builder.Services.AddEmailTwoFactor(opt =>
{
    opt.Subject = "رمز التحقق - عشير";
    opt.BodyTemplate = "رمز التحقق الخاص بك في عشير: {0}\nصالح لمدة 10 دقائق.";
});

var nafathClientId = builder.Configuration["Authentication:TwoFactor:Nafath:ClientId"];
if (!string.IsNullOrWhiteSpace(nafathClientId) && !nafathClientId.Contains("${"))
{
    Log.Information("Nafath 2FA enabled");
    builder.Services.AddNafathTwoFactor(builder.Configuration);
}
else
{
    Log.Warning("Nafath credentials missing - Nafath 2FA disabled");
}

builder.Services.AddSingleton<TwoFactorConfig>(sp =>
{
    var cfg = new TwoFactorConfig();
    foreach (var ch in sp.GetServices<ITwoFactorChannel>())
        cfg.AddChannel(ch);
    return cfg;
});
builder.Services.AddScoped<TwoFactorService>();

// ─────────────────────────────────────────────────────────
// Payments: Noon (Live في الإنتاج)
// ─────────────────────────────────────────────────────────
var noonOptions = new NoonOptions
{
    BusinessIdentifier = builder.Configuration["Payments:Noon:BusinessIdentifier"] ?? "ashier",
    ApplicationIdentifier = builder.Configuration["Payments:Noon:ApplicationIdentifier"] ?? "newAshier",
    ApiKey = builder.Configuration["Payments:Noon:ApiKey"] ?? "",
    Mode = (builder.Configuration["Payments:Noon:Mode"]?.Equals("Live", StringComparison.OrdinalIgnoreCase) ?? false)
        ? NoonMode.Live : NoonMode.Test,
    DefaultCurrency = builder.Configuration["Payments:Noon:DefaultCurrency"] ?? "SAR",
    Channel = builder.Configuration["Payments:Noon:Channel"] ?? "Web",
    WebhookSecret = builder.Configuration["Payments:Noon:WebhookSecret"]
};
builder.Services.AddSingleton(noonOptions);
builder.Services.AddHttpClient<NoonPaymentGateway>();
builder.Services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<NoonPaymentGateway>());

builder.Services.AddSingleton<PaymentConfig>(sp =>
{
    var cfg = new PaymentConfig();
    foreach (var gw in sp.GetServices<IPaymentGateway>())
        cfg.AddGateway(gw);
    return cfg;
});
builder.Services.AddScoped<PaymentService>();

// ─────────────────────────────────────────────────────────
// Translations + Favorites
// ─────────────────────────────────────────────────────────
builder.Services.AddTranslationOperations();
builder.Services.AddFavoriteOperations();

// ─────────────────────────────────────────────────────────
// Subscriptions (general lib + cross-cutting interceptors)
// ─────────────────────────────────────────────────────────
EntityDiscoveryRegistry.RegisterEntity(typeof(Plan));
EntityDiscoveryRegistry.RegisterEntity(typeof(Subscription));

// مزوّد الاشتراكات الـAshare-specific - يجسر كيانات Ashare بواجهات IPlan/ISubscription
builder.Services.AddScoped<ACommerce.Subscriptions.Operations.Abstractions.ISubscriptionProvider,
    AshareSubscriptionProvider>();

// تسجيل المعترضات في DI
builder.Services.AddScoped<ACommerce.Subscriptions.Operations.QuotaInterceptor>();
builder.Services.AddScoped<ACommerce.Subscriptions.Operations.QuotaConsumptionInterceptor>();

// تسجيل سجل المعترضات + ربط معترضات الاشتراكات
// نستخدم singleton wrappers تستهلك Scoped عبر IServiceScopeFactory
builder.Services.AddOperationInterceptors(registry =>
{
    // المعترضات نفسها Scoped (تحتاج DbContext) - لذا نلفّها بـ adapter ينشئها لكل عملية
    registry.Register(new ACommerce.OperationEngine.Interceptors.PredicateInterceptor(
        name: "QuotaInterceptor",
        phase: ACommerce.OperationEngine.Interceptors.InterceptorPhase.Pre,
        appliesTo: op => op.HasTag("quota_check"),
        intercept: async (ctx, _) =>
        {
            var sp = ctx.Services;
            var inner = sp.GetRequiredService<ACommerce.Subscriptions.Operations.QuotaInterceptor>();
            return await inner.InterceptAsync(ctx, null);
        }));

    registry.Register(new ACommerce.OperationEngine.Interceptors.PredicateInterceptor(
        name: "QuotaConsumptionInterceptor",
        phase: ACommerce.OperationEngine.Interceptors.InterceptorPhase.Post,
        appliesTo: op => op.HasTag("quota_check"),
        intercept: async (ctx, _) =>
        {
            var sp = ctx.Services;
            var inner = sp.GetRequiredService<ACommerce.Subscriptions.Operations.QuotaConsumptionInterceptor>();
            return await inner.InterceptAsync(ctx, null);
        }));
});

// ─────────────────────────────────────────────────────────
// Rate Limiting (ASP.NET Core 8+)
// ─────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(
        httpContext => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("strict", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "rate_limit_exceeded",
            retryAfterSeconds = 60
        }, ct);
    };
});

// ─────────────────────────────────────────────────────────
// HSTS (Strict Transport Security)
// ─────────────────────────────────────────────────────────
if (builder.Environment.IsProduction())
{
    builder.Services.AddHsts(options =>
    {
        options.Preload = true;
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(365);
    });

    builder.Services.AddHttpsRedirection(options =>
    {
        options.HttpsPort = 443;
    });
}

// ─────────────────────────────────────────────────────────
// Seeder
// ─────────────────────────────────────────────────────────
builder.Services.AddScoped<AshareSeeder>();

// ─────────────────────────────────────────────────────────
// Build app
// ─────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "Ashare.Api",
    description = "خدمة عشير الخلفية المبنية على OperationEngine المحاسبي",
    version = "1.1.0",
    environment = app.Environment.EnvironmentName,
    docs = "/swagger"
}));

app.MapGet("/health", (IServiceProvider sp) => Results.Ok(new
{
    status = "healthy",
    time = DateTime.UtcNow,
    db = builder.Configuration["Database:Provider"] ?? "InMemory",
    storage = builder.Configuration["Files:Storage:Provider"] ?? "None",
    storageRegistered = sp.GetService<IStorageProvider>() != null,
    nafathEnabled = sp.GetServices<ITwoFactorChannel>().Any(c => c.Name == "nafath"),
    firebaseEnabled = sp.GetServices<INotificationChannel>().Any(c => c.ChannelName == "firebase")
}));

// === DB schema + seeding ===
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider
        .GetRequiredService<ACommerce.SharedKernel.Infrastructure.EFCores.Context.ApplicationDbContext>();
    // SQLite dev mode: detect schema drift (entity changed without migration)
    // and reset the file so EnsureCreatedAsync can recreate everything fresh.
    if (ACommerce.SharedKernel.Infrastructure.EFCores.SqliteSchemaGuard.ResetIfDrifted(db))
        Log.Warning("Ashare.Api: SQLite schema drift detected — DB file rebuilt");
    // EnsureCreatedAsync creates all-or-nothing; when another platform
    // backend created tables first it throws.  Treat that as benign so
    // we still run the seeder below.
    try { await db.Database.EnsureCreatedAsync(); }
    catch (Exception schemaEx) { Log.Information(schemaEx, "Schema already created by another service — continuing"); }
    ACommerce.SharedKernel.Infrastructure.EFCores.SqliteSchemaGuard.StampFingerprint(db);
    Log.Information("Ashare.Api database schema ready");

    var seeder = scope.ServiceProvider.GetRequiredService<AshareSeeder>();
    await seeder.SeedAsync();
}
catch (Exception ex)
{
    Log.Error(ex, "Seeding failed (DB may be unreachable)");
}

app.Run();
