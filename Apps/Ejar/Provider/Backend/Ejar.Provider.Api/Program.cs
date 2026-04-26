using ACommerce.Authentication.TwoFactor.Providers.Sms.Mock.Extensions;
using ACommerce.Cache.Providers.InMemory.Extensions;
using ACommerce.Cache.Providers.Redis.Extensions;
using ACommerce.Kits.Auth.Sms.Backend;
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
using Ejar.Provider.Api.Stores;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/ejar-provider-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    Log.Information("Starting Ejar Provider API...");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();
    var cfg = builder.Configuration;
    var env = builder.Environment;

    // ─── MVC + Swagger ──────────────────────────────────────────────────────
    builder.Services.AddControllers()
        .PartManager.ApplicationParts.Clear();
    builder.Services.AddControllers()
        .PartManager.ApplicationParts.Add(
            new Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart(typeof(Program).Assembly));
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Ejar Provider API — مزود إيجار", Version = "v1" });
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

    // ─── CORS — AllowCredentials required for SignalR WebSocket ─────────────
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    {
        if (env.IsDevelopment())
            p.WithOrigins("http://localhost:5311", "https://localhost:5311")
             .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        else
        {
            var origins = cfg.GetSection("Cors:AllowedOrigins").Get<string[]>()
                          ?? ["https://provider.ejar.app"];
            p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
    }));

    // ─── JWT ────────────────────────────────────────────────────────────────
    var jwtSecret   = cfg["JWT:SecretKey"]  ?? "Ejar-provider-dev-secret-min-32chars-here!!!!";
    var jwtIssuer   = cfg["JWT:Issuer"]     ?? "http://localhost:5310";
    var jwtAudience = cfg["JWT:Audience"]   ?? "ejar-provider-api";
    if (jwtSecret.Contains("dev-secret"))
        Log.Warning("Ejar.Provider: JWT:SecretKey is a development placeholder — set a real secret in production.");

    // (EjarProviderJwtConfig record removed — the Auth Kit owns its own
    //  AuthSmsKitJwtConfig; we hand it the raw values further below.)

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

    // ─── OAM engine + interceptors ──────────────────────────────────────────
    builder.Services.AddScoped<OpEngine>(sp =>
        new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

    builder.Services.AddSingleton<OperationLogInterceptor>();
    builder.Services.AddSingleton<IOperationInterceptor>(
        sp => sp.GetRequiredService<OperationLogInterceptor>());
    builder.Services.AddOperationInterceptors();

    // ─── Auth Kit (drop-in /auth/otp/{request,verify} + /auth/logout) ──────
    builder.Services.AddMockSmsTwoFactor();
    builder.Services.AddSmsAuthKit<EjarProviderAuthUserStore>(
        new AuthSmsKitJwtConfig(
            Secret:    jwtSecret,
            Issuer:    jwtIssuer,
            Audience:  jwtAudience,
            Role:      "provider",
            PartyKind: "Provider"));

    // ─── Realtime + Chat Kit (drop-in /conversations + /chat/{id}/enter|leave) ─
    builder.Services.AddSignalRRealtimeTransport();
    builder.Services.AddScoped<RealtimeService>();
    builder.Services.AddInAppNotificationChannel(o => o.MethodName = "ReceiveNotification");
    builder.Services.AddChatKit<EjarProviderChatStore>(o => o.PartyKind = "Provider");

    var firebaseJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON")
                       ?? cfg["Notifications:Firebase:ServiceAccountKeyJson"];
    if (!string.IsNullOrEmpty(firebaseJson))
        builder.Services.AddFirebaseNotificationChannel(cfg);

    // REMINDER: set Cache:Redis:ConnectionString (and optionally
    // Realtime:Redis:ConnectionString — falls back to the cache one) before
    // multi-instance deployment. Same Redis as Ejar Customer/Admin so chat
    // state is shared across all hubs.
    {
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
            builder.Services.AddSingleton<IConnectionTracker>(sp => sp.GetRequiredService<InMemoryConnectionTracker>());
        }
        if (!string.IsNullOrEmpty(rtRedis))
            builder.Services.AddSignalRRedisBackplane(rtRedis);
    }

    var app = builder.Build();
    app.Services.WireChatNotificationCoupling();

    if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error");
    app.UseGlobalExceptionHandler();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseCurrentUser();
    app.UseCurrentCulture();

    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ejar Provider API v1"));

    app.MapControllers();
    app.MapHealthChecks("/healthz");
    app.MapHub<AShareHub>("/hubs/ejar-provider");

    app.MapGet("/", () => Results.Ok(new
    {
        service     = "Ejar.Provider.Api",
        description = "خدمة مزود إيجار — يدير الإعلانات والمحادثات مع المستأجرين",
        version     = "1.0.0"
    }));

    Log.Information("Ejar Provider API ready [{Env}]", env.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Ejar Provider API failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
