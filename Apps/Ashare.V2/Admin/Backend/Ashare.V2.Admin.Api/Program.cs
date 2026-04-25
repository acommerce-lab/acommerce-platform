using System.Text;
using ACommerce.Authentication.TwoFactor.Operations;
using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.Authentication.TwoFactor.Providers.Sms.Mock.Extensions;
using ACommerce.Chat.Operations;
using ACommerce.Notification.Providers.InApp.Extensions;
using ACommerce.OperationEngine.Core;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.Realtime.Providers.InMemory;
using ACommerce.Realtime.Providers.SignalR;
using ACommerce.Realtime.Providers.SignalR.Extensions;
using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;
using Ashare.V2.Api.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/ashare-v2-admin-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    Log.Information("Starting Ashare V2 Admin API...");

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
    var cfg = builder.Configuration;
    var env = builder.Environment;

    // ─── MVC + Swagger ────────────────────────────────────────────────────
    // Clear application parts to avoid controller conflicts with Ashare.V2.Api
    builder.Services.AddControllers()
        .PartManager.ApplicationParts.Clear();
    builder.Services.AddControllers()
        .PartManager.ApplicationParts.Add(
            new Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart(typeof(Program).Assembly));
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
        c.SwaggerDoc("v1", new() { Title = "Ashare V2 Admin API", Version = "v1" }));

    // ─── CORS ─────────────────────────────────────────────────────────────
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    {
        var origins = cfg.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? (env.IsDevelopment() ? ["http://localhost:5603"] : ["https://admin.ashare.app"]);
        p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }));

    // ─── Database (shares file with Ashare.V2.Api) ────────────────────────
    var connStr = cfg["Database:ConnectionString"];
    if (string.IsNullOrEmpty(connStr))
        connStr = ACommerce.SharedKernel.Infrastructure.EFCores.PlatformDataRoot
            .SqliteConnectionString(builder.Environment.ContentRootPath, "ashare-v2-platform.db");
    builder.Services.AddACommerceSQLite(connStr);

    // ─── OAM Engine ───────────────────────────────────────────────────────
    builder.Services.AddScoped<OpEngine>(sp =>
        new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

    // ─── JWT config ───────────────────────────────────────────────────────
    var jwtKey    = cfg["JWT:SecretKey"]  ?? "Ashare-V2-Admin-dev-secret-do-not-use-in-prod-32chars!";
    var jwtIssuer = cfg["JWT:Issuer"]     ?? "http://localhost:5503";
    var jwtAud    = cfg["JWT:Audience"]   ?? "ashare-v2-admin-api-dev";
    builder.Services.AddSingleton(new AdminV2JwtConfig(jwtKey, jwtIssuer, jwtAud));

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
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
            o.Events = new JwtBearerEvents
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

    builder.Services.AddAuthorization(opts =>
        opts.AddPolicy("AdminOnly", p =>
            p.RequireAuthenticatedUser().RequireClaim("role", "admin")));

    // ─── 2FA: SMS Mock (auto-verify — swap for real provider in prod) ─────
    builder.Services.AddMockSmsTwoFactor();
    builder.Services.AddSingleton<TwoFactorConfig>(sp =>
    {
        var c = new TwoFactorConfig();
        foreach (var ch in sp.GetServices<ITwoFactorChannel>()) c.AddChannel(ch);
        return c;
    });
    builder.Services.AddScoped<TwoFactorService>();

    // ─── Realtime + Chat + InApp notifications (abstractions only) ────────
    builder.Services.AddSignalRRealtimeTransport();
    builder.Services.AddSingleton<InMemoryConnectionTracker>();
    builder.Services.AddSingleton<IConnectionTracker>(sp => sp.GetRequiredService<InMemoryConnectionTracker>());
    builder.Services.AddInAppNotificationChannel(o => o.MethodName = "ReceiveNotification");
    builder.Services.AddChat();

    var app = builder.Build();

    // Chat<->Notifications coupling per standard policy.
    app.Services.WireChatNotificationCoupling();

    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ashare V2 Admin API v1"));
    app.MapControllers();
    app.MapHub<AShareHub>("/hubs/ashare-v2-admin");
    app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", service = "Ashare.V2.Admin.Api" }));

    // ─── Ensure shared DB schema ──────────────────────────────────────────
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<ACommerce.SharedKernel.Infrastructure.EFCores.Context.ApplicationDbContext>();
        if (ACommerce.SharedKernel.Infrastructure.EFCores.SqliteSchemaGuard.ResetIfDrifted(db))
            Log.Warning("Ashare.V2.Admin.Api: SQLite schema drift — rebuilt");
        try { await db.Database.EnsureCreatedAsync(); } catch { }
        ACommerce.SharedKernel.Infrastructure.EFCores.SqliteSchemaGuard.StampFingerprint(db);
    }
    catch (Exception ex) { Log.Error(ex, "DB init failed"); }

    Log.Information("Ashare V2 Admin API ready [{Env}]", env.EnvironmentName);
    app.Run();
}
catch (Exception ex) { Log.Fatal(ex, "Ashare V2 Admin API failed to start"); throw; }
finally { Log.CloseAndFlush(); }

public partial class Program { }
public record AdminV2JwtConfig(string Secret, string Issuer, string Audience);
