using ACommerce.Authentication.TwoFactor.Providers.Sms.Mock.Extensions;
using ACommerce.Cache.Providers.InMemory.Extensions;
using ACommerce.Cache.Providers.Redis.Extensions;
using ACommerce.Chat.Operations;
using ACommerce.Kits.Auth.Backend;
using ACommerce.Kits.Auth.Operations;
using ACommerce.Kits.Auth.TwoFactor.AsAuth;
using ACommerce.Kits.Chat.Backend;
using ACommerce.Notification.Providers.Firebase.Extensions;
using ACommerce.Notification.Providers.InApp.Extensions;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.OperationEngine.Interceptors.Extensions;
using ACommerce.Realtime.Operations;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.Realtime.Providers.InMemory;
using ACommerce.Realtime.Providers.SignalR;
using ACommerce.Realtime.Providers.SignalR.Extensions;
using ACommerce.Realtime.Providers.SignalR.Redis.Extensions;
using Ejar.Api.Interceptors;
using Ejar.Api.Middleware;
using Ejar.Api.Stores;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

// ─── Bootstrap logger ──────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/ejar-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    Log.Information("Starting Ejar API...");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();
    var cfg = builder.Configuration;
    var env = builder.Environment;

    // ─── MVC + Swagger ─────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Ejar API — إيجار", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer", BearerFormat = "JWT"
        });
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            { new Microsoft.OpenApi.Models.OpenApiSecurityScheme {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
              Array.Empty<string>() }
        });
    });

    builder.Services.AddHealthChecks();
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpContextAccessor();

    // ─── CORS — AllowCredentials required for SignalR WebSocket ───────────
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    {
        if (env.IsDevelopment())
            p.WithOrigins("http://localhost:5301", "https://localhost:5301")
             .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        else
        {
            var origins = cfg.GetSection("Cors:AllowedOrigins").Get<string[]>()
                          ?? ["https://ejar.app"];
            p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
    }));

    // ─── JWT ───────────────────────────────────────────────────────────────
    var jwtSecret   = cfg["JWT:SecretKey"]  ?? "Ejar-dev-secret-do-not-use-in-prod-32chars!!!!";
    var jwtIssuer   = cfg["JWT:Issuer"]     ?? "http://localhost:5300";
    var jwtAudience = cfg["JWT:Audience"]   ?? "ejar-api";
    if (jwtSecret.Contains("dev-secret"))
        Log.Warning("Ejar: JWT:SecretKey is using a development placeholder — set a real secret in production.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey  = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer    = true, ValidIssuer    = jwtIssuer,
                ValidateAudience  = true, ValidAudience  = jwtAudience,
                ValidateLifetime  = true,
                ClockSkew         = TimeSpan.FromMinutes(2)
            };
            // SignalR WebSocket can't set HTTP headers; token comes as ?access_token=
            opts.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    var t = ctx.Request.Query["access_token"].ToString();
                    if (!string.IsNullOrEmpty(t) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        ctx.Token = t;
                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization();

    // ─── OAM engine + interceptors ─────────────────────────────────────────
    builder.Services.AddScoped<OpEngine>(sp =>
        new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));
    builder.Services.AddSingleton<OperationLogInterceptor>();
    builder.Services.AddSingleton<IOperationInterceptor>(
        sp => sp.GetRequiredService<OperationLogInterceptor>());
    builder.Services.AddOperationInterceptors();

    // ─── Auth Kit (drop-in /auth/otp/{request,verify} + /auth/logout) ────
    // للإنتاج: أبدل AddMockSmsTwoFactor() بـ AddSmsTwoFactor() مع مزوّد حقيقي.
    builder.Services.AddMockSmsTwoFactor();
    builder.Services.AddAuthKit<EjarCustomerAuthUserStore>(
        new AuthKitJwtConfig(
            Secret:    jwtSecret,
            Issuer:    jwtIssuer,
            Audience:  jwtAudience,
            Role:      "customer",
            PartyKind: "User"));
    builder.Services.AddTwoFactorAsAuth(); // bridge ITwoFactorChannel -> IAuthFlow

    // ─── Realtime + Chat Kit (drop-in /conversations + /chat/{id}/enter|leave) ─
    builder.Services.AddSignalRRealtimeTransport();
    builder.Services.AddScoped<RealtimeService>();
    builder.Services.AddInAppNotificationChannel(o => o.MethodName = "ReceiveNotification");
    builder.Services.AddChatKit<EjarCustomerChatStore>(o => o.PartyKind = "User");

    // Firebase Push (Android/iOS) — مفعّل فقط لو FIREBASE_SERVICE_ACCOUNT_JSON موجود.
    var firebaseJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON")
                       ?? cfg["Notifications:Firebase:ServiceAccountKeyJson"];
    if (!string.IsNullOrEmpty(firebaseJson))
    {
        builder.Services.AddFirebaseNotificationChannel(cfg);
        Log.Information("Ejar: Firebase FCM configured");
    }

    // ── Cache + cluster-ready realtime (opt-in via config) ─────────────────
    // REMINDER: set Cache:Redis:ConnectionString (and optionally
    // Realtime:Redis:ConnectionString — falls back to the cache one) قبل النشر
    // multi-instance. بدونها يعمل single-instance بـ InMemoryCache + InMemoryConnectionTracker.
    var cacheRedis = cfg["Cache:Redis:ConnectionString"];
    var rtRedis    = cfg["Realtime:Redis:ConnectionString"] ?? cacheRedis;
    if (!string.IsNullOrEmpty(cacheRedis))
    {
        builder.Services.AddRedisCache(cacheRedis);
        builder.Services.AddRedisConnectionTracker();
        Log.Information("Cache: Redis enabled");
    }
    else
    {
        builder.Services.AddInMemoryCache();
        builder.Services.AddSingleton<InMemoryConnectionTracker>();
        builder.Services.AddSingleton<IConnectionTracker>(
            sp => sp.GetRequiredService<InMemoryConnectionTracker>());
        Log.Information("Cache: in-memory (single-instance only)");
    }
    if (!string.IsNullOrEmpty(rtRedis))
    {
        builder.Services.AddSignalRRedisBackplane(rtRedis);
        Log.Information("Realtime: SignalR Redis backplane enabled");
    }

    // ─── Build ─────────────────────────────────────────────────────────────
    var app = builder.Build();

    // chat<->notif coupling: open chat:conv:X → mute notif:conv:X لنفس المستخدم.
    app.Services.WireChatNotificationCoupling();

    app.UseGlobalExceptionHandler();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseCurrentUser();
    app.UseCurrentCulture();

    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ejar API v1"));

    app.MapControllers();
    app.MapHealthChecks("/healthz");
    app.MapHub<AShareHub>("/hubs/ejar");

    app.MapGet("/", () => Results.Ok(new
    {
        service     = "Ejar.Api",
        description = "خدمة إيجار — عقارات الإيجار بالشهري والسنوي واليومي والساعي",
        version     = "1.0.0",
        docs        = "/swagger"
    }));

    Log.Information("Ejar API ready [{Env}]", env.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Ejar API failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
