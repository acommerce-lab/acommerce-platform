using System.Text;
using ACommerce.Authentication.TwoFactor.Providers.Nafath.Mock.Extensions;
using ACommerce.Files.Storage.AliyunOSS.Extensions;
using ACommerce.Files.Storage.Local.Extensions;
using ACommerce.Notification.Providers.Email.Extensions;
using ACommerce.Notification.Providers.Firebase.Extensions;
using ACommerce.Notification.Providers.InApp.Extensions;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.OperationEngine.Interceptors.Extensions;
using ACommerce.Payments.Providers.Noon.Extensions;
using ACommerce.Realtime.Providers.InMemory.Extensions;
using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;
using Ashare.V2.Api.Entities;
using Ashare.V2.Api.Interceptors;
using Ashare.V2.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// ─── Bootstrap logger ────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/ashare-v2-.log", rollingInterval: RollingInterval.Day,
                  retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    Log.Information("Starting Ashare V2 API...");

    // ─── Entity discovery (must run before DbContext) ─────────────────────────
    EntityDiscoveryRegistry.RegisterEntity(typeof(Profile));
    EntityDiscoveryRegistry.RegisterEntity(typeof(TwoFactorChallengeRecord));
    EntityDiscoveryRegistry.RegisterEntity(typeof(DeviceTokenEntity));
    EntityDiscoveryRegistry.RegisterEntity(typeof(ProductCategory));
    EntityDiscoveryRegistry.RegisterEntity(typeof(AttributeDefinition));
    EntityDiscoveryRegistry.RegisterEntity(typeof(CategoryAttributeMapping));
    EntityDiscoveryRegistry.RegisterEntity(typeof(Product));
    EntityDiscoveryRegistry.RegisterEntity(typeof(ProductListing));
    EntityDiscoveryRegistry.RegisterEntity(typeof(Order));
    EntityDiscoveryRegistry.RegisterEntity(typeof(Booking));
    EntityDiscoveryRegistry.RegisterEntity(typeof(Payment));
    EntityDiscoveryRegistry.RegisterEntity(typeof(Subscription));
    EntityDiscoveryRegistry.RegisterEntity(typeof(Chat));
    EntityDiscoveryRegistry.RegisterEntity(typeof(ChatMessage));
    EntityDiscoveryRegistry.RegisterEntity(typeof(Notification));
    EntityDiscoveryRegistry.RegisterEntity(typeof(Complaint));
    EntityDiscoveryRegistry.RegisterEntity(typeof(ComplaintReply));
    EntityDiscoveryRegistry.RegisterEntity(typeof(LegalPage));
    EntityDiscoveryRegistry.RegisterEntity(typeof(AttributionSession));

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    var env = builder.Environment;
    var cfg = builder.Configuration;

    // ─── MVC + Swagger ────────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
        c.SwaggerDoc("v2", new() { Title = "Ashare V2 API", Version = "v2" }));

    // ─── Health checks + infrastructure ───────────────────────────────────────
    builder.Services.AddHealthChecks();
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpContextAccessor();

    // ─── CORS ─────────────────────────────────────────────────────────────────
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    {
        if (env.IsDevelopment())
            p.WithOrigins("http://localhost:5900", "https://localhost:5901",
                          "http://localhost:5000")
             .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        else
        {
            var origins = cfg.GetSection("Cors:AllowedOrigins").Get<string[]>()
                          ?? ["https://ashare.app", "https://www.ashare.app"];
            p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }
    }));

    // ─── Database ─────────────────────────────────────────────────────────────
    // Development: SQLite file  |  Production: SQL Server (or PostgreSQL)
    var connStr = cfg.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connStr))
    {
        if (env.IsDevelopment() || connStr.StartsWith("Data Source="))
        {
            builder.Services.AddACommerceSQLite(connStr);
            Log.Information("Database: SQLite");
        }
        else
        {
            builder.Services.AddACommerceSqlServer(connStr);
            Log.Information("Database: SQL Server");
        }
    }
    else
    {
        Log.Warning("No ConnectionStrings:DefaultConnection — running with in-memory seed data");
    }

    // ─── JWT Bearer authentication ────────────────────────────────────────────
    var jwtKey    = cfg["JWT:SecretKey"] ?? "dev-only-secret-key-32-chars-min!!";
    var jwtIssuer = cfg["JWT:Issuer"]    ?? "https://ashare.app";
    var jwtAud    = cfg["JWT:Audience"]  ?? "ashare-api";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateIssuer           = true,
                ValidIssuer              = jwtIssuer,
                ValidateAudience         = true,
                ValidAudience            = jwtAud,
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.FromMinutes(2),
            };
        });
    builder.Services.AddAuthorization();

    // ─── OAM engine ───────────────────────────────────────────────────────────
    builder.Services.AddScoped<OpEngine>(sp =>
        new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

    // ─── Interceptors ─────────────────────────────────────────────────────────
    builder.Services.AddSingleton<OperationLogInterceptor>();
    builder.Services.AddOperationInterceptors(r =>
    {
        r.Register(new OwnershipInterceptor());
        r.Register(new ListingQuotaInterceptor());
        r.Register(new PersistenceInterceptor());
    });
    builder.Services.AddSingleton<ACommerce.OperationEngine.Interceptors.IOperationInterceptor>(
        sp => sp.GetRequiredService<OperationLogInterceptor>());

    // ─── JWT config record (يُحقَن في AuthController) ────────────────────────
    builder.Services.AddSingleton(new AshareV2JwtConfig(jwtKey, jwtIssuer, jwtAud));

    // ─── Nafath Mock 2FA (تحقق تلقائي بعد 10 ث — يُستبدَل بـ AddNafathTwoFactor في الإنتاج) ──
    builder.Services.AddMockNafathTwoFactor();
    Log.Information("Nafath 2FA: Mock channel (auto-verify after 10 s)");

    // ─── Noon Pay (payment gateway) ───────────────────────────────────────────
    var noonCfg = cfg.GetSection("Payments:Noon");
    if (noonCfg.Exists() && !string.IsNullOrEmpty(noonCfg["ApplicationKey"]))
    {
        builder.Services.AddNoonPaymentGateway(cfg);
        Log.Information("Noon Pay: configured (IsSandbox={IsSandbox})", noonCfg["IsSandbox"]);
    }
    else
    {
        Log.Warning("Noon Pay: not configured — set Payments:Noon:ApplicationKey");
    }

    // ─── File storage ─────────────────────────────────────────────────────────
    var storageProv = cfg["Files:Storage:Provider"] ?? "Local";
    if (storageProv.Equals("AliyunOSS", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddAliyunOSSFileStorage(cfg);
        Log.Information("File storage: Alibaba Cloud OSS (bucket={Bucket})",
                        cfg["Files:Storage:AliyunOSS:BucketName"]);
    }
    else
    {
        builder.Services.AddLocalFileStorage(cfg);
        Log.Information("File storage: Local (dev)");
    }

    // ─── Notifications ────────────────────────────────────────────────────────
    builder.Services.AddInMemoryRealtimeTransport();
    builder.Services.AddInAppNotificationChannel();

    var firebaseJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON")
                       ?? cfg["Notifications:Firebase:ServiceAccountKeyJson"];
    if (!string.IsNullOrEmpty(firebaseJson))
    {
        builder.Services.AddFirebaseNotificationChannel(cfg);
        Log.Information("Notifications: Firebase FCM configured");
    }
    else
    {
        Log.Warning("Notifications: Firebase not configured — set FIREBASE_SERVICE_ACCOUNT_JSON");
    }

    if (!string.IsNullOrEmpty(cfg["Email:Smtp:Host"]))
    {
        builder.Services.AddEmailNotifications(cfg);
        Log.Information("Notifications: Email via {Host}", cfg["Email:Smtp:Host"]);
    }

    // ─── Build ────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ─── Middleware pipeline ──────────────────────────────────────────────────
    app.UseGlobalExceptionHandler();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseCurrentUser();
    app.UseCurrentCulture();

    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v2/swagger.json", "Ashare V2 API v2"));

    app.MapControllers();
    app.MapHealthChecks("/healthz");

    // ─── Restore seed snapshot (in-memory mode only) ──────────────────────────
    await Ashare.V2.Api.Services.JsonSnapshotStore.RestoreAsync();

    Log.Information("Ashare V2 API ready [{Env}]", env.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Ashare V2 API failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }

// ─── config record ────────────────────────────────────────────────────────────
public record AshareV2JwtConfig(string Secret, string Issuer, string Audience);
