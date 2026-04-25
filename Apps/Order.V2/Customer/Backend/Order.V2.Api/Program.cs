using ACommerce.Authentication.Operations;
using ACommerce.Authentication.Operations.Abstractions;
using ACommerce.Authentication.Providers.Token;
using ACommerce.Authentication.Providers.Token.Extensions;
using ACommerce.Authentication.TwoFactor.Operations;
using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.Authentication.TwoFactor.Providers.Sms.Mock.Extensions;
using ACommerce.Culture.Interceptors;
using ACommerce.Notification.Operations;
using ACommerce.Notification.Operations.Abstractions;
using ACommerce.Notification.Providers.InApp.Extensions;
using ACommerce.OperationEngine.Core;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.Cache.Operations.Abstractions;
using ACommerce.Cache.Providers.InMemory.Extensions;
using ACommerce.Cache.Providers.Redis.Extensions;
using ACommerce.Realtime.Providers.InMemory;          // InMemoryConnectionTracker (single-instance fallback)
using ACommerce.Realtime.Providers.InMemory.Extensions;
using ACommerce.Realtime.Providers.SignalR;
using ACommerce.Realtime.Providers.SignalR.Extensions;
using ACommerce.Realtime.Providers.SignalR.Redis.Extensions;
using ACommerce.Notification.Providers.Firebase.Extensions;
using ACommerce.Chat.Operations;
using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;
using Order.V2.Api.Entities;
using Order.V2.Api.Services;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;
var env = builder.Environment;

// ─────────────────────────────────────────────────────────
// Serilog: console + rolling file log
// ─────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(cfg)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/order-v2-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
builder.Host.UseSerilog();

// ─────────────────────────────────────────────────────────
// Entity discovery
// ─────────────────────────────────────────────────────────
EntityDiscoveryRegistry.RegisterEntity(typeof(User));
EntityDiscoveryRegistry.RegisterEntity(typeof(Category));
EntityDiscoveryRegistry.RegisterEntity(typeof(Vendor));
EntityDiscoveryRegistry.RegisterEntity(typeof(Offer));
EntityDiscoveryRegistry.RegisterEntity(typeof(OrderRecord));
EntityDiscoveryRegistry.RegisterEntity(typeof(OrderItem));
EntityDiscoveryRegistry.RegisterEntity(typeof(Conversation));
EntityDiscoveryRegistry.RegisterEntity(typeof(Message));
EntityDiscoveryRegistry.RegisterEntity(typeof(Notification));
EntityDiscoveryRegistry.RegisterEntity(typeof(Favorite));
EntityDiscoveryRegistry.RegisterEntity(typeof(TwoFactorChallengeRecord));

// Shared platform entities
EntityDiscoveryRegistry.RegisterEntity(typeof(ACommerce.OrderPlatform.Entities.VendorUser));
EntityDiscoveryRegistry.RegisterEntity(typeof(ACommerce.OrderPlatform.Entities.VendorSettings));
EntityDiscoveryRegistry.RegisterEntity(typeof(ACommerce.OrderPlatform.Entities.WorkSchedule));
EntityDiscoveryRegistry.RegisterEntity(typeof(ACommerce.OrderPlatform.Entities.IncomingOrder));

// ─────────────────────────────────────────────────────────
// Database: SQLite (dev) / SQL Server (prod) — conditional
// ─────────────────────────────────────────────────────────
var dbProvider = cfg["Database:Provider"] ?? "SQLite";
var dbConnection = cfg["Database:ConnectionString"];

switch (dbProvider.ToLowerInvariant())
{
    case "sqlserver":
    case "mssql":
        if (string.IsNullOrWhiteSpace(dbConnection))
            Log.Warning("Order.V2: SQL Server selected but Database:ConnectionString is empty. Check your env vars.");
        else
            builder.Services.AddACommerceSqlServer(dbConnection);
        break;

    default: // SQLite
        var sqliteConn = !string.IsNullOrWhiteSpace(dbConnection)
            ? dbConnection
            : ACommerce.SharedKernel.Infrastructure.EFCores.PlatformDataRoot
                .SqliteConnectionString(builder.Environment.ContentRootPath, "order-v2.db");
        builder.Services.AddACommerceSQLite(sqliteConn);
        break;
}

// ─────────────────────────────────────────────────────────
// Culture + MVC + Swagger + CORS
// ─────────────────────────────────────────────────────────
builder.Services.AddCultureStack();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Order V2 API", Version = "v2" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var allowedOrigins = cfg.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:5702" };
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// ─────────────────────────────────────────────────────────
// OperationEngine
// ─────────────────────────────────────────────────────────
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// ─────────────────────────────────────────────────────────
// Realtime + Notifications + Chat
// SignalR is the real-world transport (browser push via WebSocket).
// InMemoryConnectionTracker maps userId → connectionId (swap for Redis at scale).
// ─────────────────────────────────────────────────────────
builder.Services.AddSignalRRealtimeTransport();

// ── Cache + cluster-ready realtime (opt-in via config) ─────────────────────
// REMINDER: set Cache:Redis:ConnectionString in appsettings.{Env}.json
// (and optionally Realtime:Redis:ConnectionString — falls back to the cache one)
// before deploying multi-instance. Without it, runs single-instance with
// in-memory cache + per-process tracker.
{
    var cacheRedis = builder.Configuration["Cache:Redis:ConnectionString"];
    var rtRedis    = builder.Configuration["Realtime:Redis:ConnectionString"] ?? cacheRedis;
    if (!string.IsNullOrEmpty(cacheRedis))
    {
        builder.Services.AddRedisCache(cacheRedis);
        builder.Services.AddRedisConnectionTracker();
    }
    else
    {
        builder.Services.AddInMemoryCache();
        builder.Services.AddSingleton<InMemoryConnectionTracker>();
        builder.Services.AddSingleton<IConnectionTracker>(sp => sp.GetRequiredService<InMemoryConnectionTracker>());
    }
    if (!string.IsNullOrEmpty(rtRedis))
        builder.Services.AddSignalRRedisBackplane(rtRedis);
}
builder.Services.AddInAppNotificationChannel(opt =>
{
    opt.MethodName = "ReceiveNotification";
    opt.AllowOffline = true;
});

// Firebase Push (opt-in). Activated only when FIREBASE_SERVICE_ACCOUNT_JSON is set.
var firebaseJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON")
                   ?? builder.Configuration["Notifications:Firebase:ServiceAccountKeyJson"];
if (!string.IsNullOrEmpty(firebaseJson))
    builder.Services.AddFirebaseNotificationChannel(builder.Configuration);

builder.Services.AddChat();

builder.Services.AddNotifications(config =>
{
    config.DefineType(OrderNotifications.NewOrder);
    config.DefineType(OrderNotifications.OrderAccepted);
    config.DefineType(OrderNotifications.OrderReady);
    config.DefineType(OrderNotifications.OrderRejected);
    config.DefineType(OrderNotifications.OrderDelivered);
});

// ─────────────────────────────────────────────────────────
// Authentication: JWT + SMS 2FA (console mock — no Nafath)
// ─────────────────────────────────────────────────────────
var jwtSecret = cfg["JWT:SecretKey"]
    ?? "Order-V2-dev-secret-do-not-use-in-prod-32chars-min!!!";
if (jwtSecret.Contains("REPLACE") || jwtSecret.Contains("dev-secret"))
    Log.Warning("Order.V2: JWT:SecretKey is using a development placeholder. Set a real secret in production.");

var jwtOptions = new JwtOptions
{
    Issuer = cfg["JWT:Issuer"] ?? "https://order.app",
    Audience = cfg["JWT:Audience"] ?? "order-api",
    SecretKey = jwtSecret,
    AccessTokenLifetime = TimeSpan.TryParse(
        cfg["JWT:AccessTokenLifetime"], out var lt) ? lt : TimeSpan.FromDays(30)
};
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtTokenStore>();
builder.Services.AddSingleton<ITokenValidator>(sp => sp.GetRequiredService<JwtTokenStore>());
builder.Services.AddSingleton<ITokenIssuer>(sp => sp.GetRequiredService<JwtTokenStore>());
builder.Services.AddTokenAuthenticator();

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
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        // SignalR WebSocket upgrades can't set HTTP headers, so pass the token
        // as ?access_token= for requests under /hubs.
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"].ToString();
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
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

// SMS 2FA — mock: رمز ثابت 123456 (يُستبدَل بـ AddSmsTwoFactor() في الإنتاج)
builder.Services.AddMockSmsTwoFactor();
builder.Services.AddSingleton<TwoFactorConfig>(sp =>
{
    var config = new TwoFactorConfig();
    foreach (var ch in sp.GetServices<ITwoFactorChannel>())
        config.AddChannel(ch);
    return config;
});
builder.Services.AddScoped<TwoFactorService>();

// ─────────────────────────────────────────────────────────
// Vendor.Api webhook client
// ─────────────────────────────────────────────────────────
var vendorApiBase = cfg["VendorApi:BaseUrl"] ?? "http://localhost:5201";
builder.Services.AddHttpClient("vendor-api", c =>
{
    c.BaseAddress = new Uri(vendorApiBase);
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<VendorApiNotifier>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<VendorApiNotifier>>();
    return new VendorApiNotifier(factory.CreateClient("vendor-api"), logger);
});

// ─────────────────────────────────────────────────────────
// Seeder
// ─────────────────────────────────────────────────────────
builder.Services.AddScoped<OrderSeeder>();

// ─────────────────────────────────────────────────────────
// Build
// ─────────────────────────────────────────────────────────
var app = builder.Build();

// Bridge notification channels into NotificationConfig
using (var chScope = app.Services.CreateScope())
{
    var notifConfig = chScope.ServiceProvider.GetRequiredService<NotificationConfig>();
    foreach (var ch in chScope.ServiceProvider.GetServices<INotificationChannel>())
        notifConfig.AddChannel(ch);
}

// Chat<->Notifications coupling: opening chat:conv:X closes notif:conv:X
// for the same user; closing chat (any reason) re-opens notif.
app.Services.WireChatNotificationCoupling();

app.UseCors();
app.UseCultureContext();
app.UseAuthentication();
app.UseAuthorization();

if (env.IsDevelopment() || cfg.GetValue<bool>("EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Order V2 API"));
}

app.MapControllers();
app.MapHub<AShareHub>("/hubs/order-v2");

app.MapGet("/", () => Results.Ok(new
{
    service = "Order.V2.Api",
    description = "خدمة اوردر V2 — مبنية على OperationEngine المحاسبي + SQLite/SQL Server",
    version = "2.0.0",
    docs = "/swagger"
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    time = DateTime.UtcNow,
    db = dbProvider
}));

// DB schema + seed
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider
        .GetRequiredService<ACommerce.SharedKernel.Infrastructure.EFCores.Context.ApplicationDbContext>();
    if (ACommerce.SharedKernel.Infrastructure.EFCores.SqliteSchemaGuard.ResetIfDrifted(db))
        Log.Warning("Order.V2.Api: SQLite schema drift detected — DB file rebuilt");
    try { await db.Database.EnsureCreatedAsync(); Log.Information("Database schema ready"); }
    catch (Exception schemaEx) { Log.Information(schemaEx, "Schema already created by another service — continuing"); }
    ACommerce.SharedKernel.Infrastructure.EFCores.SqliteSchemaGuard.StampFingerprint(db);

    var seeder = scope.ServiceProvider.GetRequiredService<OrderSeeder>();
    await seeder.SeedAsync();
    Log.Information("Seeding complete");
}
catch (Exception ex)
{
    Log.Error(ex, "Database initialisation failed");
}

app.Run();
