using ACommerce.Authentication.TwoFactor.Providers.Sms.Mock.Extensions;
using ACommerce.Notification.Providers.InApp;
using ACommerce.Realtime.Operations;
using ACommerce.Realtime.Providers.SignalR;
using ACommerce.Realtime.Providers.SignalR.Extensions;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Interceptors;
using ACommerce.OperationEngine.Interceptors.Extensions;
using Ejar.Api.Interceptors;
using Ejar.Api.Middleware;
using Ejar.Api.Services;
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

    // ─── MVC + Swagger ──────────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Ejar API — إيجار", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type   = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer", BearerFormat = "JWT"
        });
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    // ─── Health checks + cache ──────────────────────────────────────────────────
    builder.Services.AddHealthChecks();
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpContextAccessor();

    // ─── CORS — AllowCredentials is required for SignalR WebSocket ───────────
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

    // ─── JWT Bearer ─────────────────────────────────────────────────────────────
    var jwtSecret   = cfg["JWT:SecretKey"]  ?? "Ejar-dev-secret-do-not-use-in-prod-32chars!!!!";
    var jwtIssuer   = cfg["JWT:Issuer"]     ?? "http://localhost:5300";
    var jwtAudience = cfg["JWT:Audience"]   ?? "ejar-api";

    if (jwtSecret.Contains("dev-secret"))
        Log.Warning("Ejar: JWT:SecretKey is using a development placeholder — set a real secret in production.");

    builder.Services.AddSingleton(new EjarJwtConfig(jwtSecret, jwtIssuer, jwtAudience));

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
                ClockSkew = TimeSpan.FromSeconds(30)
            };
            // SignalR WebSocket connections can't set HTTP headers,
            // so the token is passed as ?access_token= in the query string.
            opts.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    var token = ctx.Request.Query["access_token"].ToString();
                    if (!string.IsNullOrEmpty(token) &&
                        ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        ctx.Token = token;
                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization();

    // ─── OAM engine ─────────────────────────────────────────────────────────────
    builder.Services.AddScoped<OpEngine>(sp =>
        new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

    // ─── Interceptors ───────────────────────────────────────────────────────────
    builder.Services.AddSingleton<OperationLogInterceptor>();
    builder.Services.AddSingleton<IOperationInterceptor>(
        sp => sp.GetRequiredService<OperationLogInterceptor>());
    builder.Services.AddOperationInterceptors();

    // ─── Auth: Mock SMS 2FA (رمز ثابت 123456) ─────────────────────────────────
    // للإنتاج: أبدل هذا السطر بـ builder.Services.AddSmsTwoFactor()
    builder.Services.AddMockSmsTwoFactor();

    // ─── Realtime: SignalR + InApp notification channel ────────────────────────
    builder.Services.AddSignalRRealtimeTransport();
    builder.Services.AddScoped<RealtimeService>();
    builder.Services.AddSingleton(new InAppOptions { MethodName = "ReceiveNotification" });
    builder.Services.AddScoped<InAppNotificationChannel>();

    // ─── Build ──────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ─── Middleware pipeline (بنفس ترتيب عشير V2) ─────────────────────────────
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

// ─── config record ───────────────────────────────────────────────────────────
public record EjarJwtConfig(string Secret, string Issuer, string Audience);
