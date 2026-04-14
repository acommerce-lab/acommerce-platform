using ACommerce.Authentication.Operations;
using ACommerce.Authentication.Operations.Abstractions;
using ACommerce.Authentication.Providers.Token;
using ACommerce.Authentication.Providers.Token.Extensions;
using ACommerce.Authentication.TwoFactor.Operations;
using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.Authentication.TwoFactor.Providers.Sms.Extensions;
using ACommerce.Notification.Operations.Abstractions;
using ACommerce.OperationEngine.Core;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.Realtime.Providers.InMemory.Extensions;
using ACommerce.Notification.Providers.InApp.Extensions;
using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Abstractions.Repositories;
using ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;
using Order.Admin.Api.Configuration;
using Order.Api.Entities;
using Order.Api.Services;
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
// Register Order entities (shared with Customer API)
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

// ─────────────────────────────────────────────────────────
// Database: SQLite بشكل افتراضي، InMemory كبديل
// ─────────────────────────────────────────────────────────
var dbProvider = builder.Configuration["Database:Provider"] ?? "SQLite";
var dbConnection = builder.Configuration["Database:ConnectionString"];

switch (dbProvider.ToLowerInvariant())
{
    case "sqlite":
        Directory.CreateDirectory("data");
        builder.Services.AddACommerceSQLite(dbConnection ?? "Data Source=data/order-admin.db");
        break;

    case "sqlserver":
        if (string.IsNullOrWhiteSpace(dbConnection) || dbConnection.Contains("${"))
        {
            Log.Warning("Database:ConnectionString غير مُعيّن في env vars - الرجوع لـ InMemory");
            builder.Services.AddACommerceInMemoryDatabase("OrderAdminDb");
        }
        else
        {
            Log.Information("Using SQL Server database");
            builder.Services.AddACommerceSqlServer(dbConnection);
        }
        break;

    default:
        Log.Information("Using InMemory database");
        builder.Services.AddACommerceInMemoryDatabase("OrderAdminDb");
        break;
}

// ─────────────────────────────────────────────────────────
// MVC + Swagger + CORS
// ─────────────────────────────────────────────────────────
// Only scan this assembly for controllers. Order.Api.dll (referenced for its
// entities) also contains an AuthController on /api/auth — scanning both
// would produce AmbiguousMatchException at runtime.
builder.Services.AddControllers()
    .PartManager.ApplicationParts.Clear();
var mvcBuilder = builder.Services.AddControllers();
mvcBuilder.PartManager.ApplicationParts.Add(
    new Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart(typeof(Program).Assembly));
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

// ─────────────────────────────────────────────────────────
// Authentication: JWT
// ─────────────────────────────────────────────────────────
var jwtOptions = new JwtOptions
{
    Issuer = builder.Configuration["JWT:Issuer"] ?? "https://order.app",
    Audience = builder.Configuration["JWT:Audience"] ?? "order-admin-api",
    SecretKey = builder.Configuration["JWT:SecretKey"]
        ?? "Order-admin-dev-secret-do-not-use-in-prod-32chars!!",
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

// Authorization: سياسة Admin تتحقق من Role = "admin"
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("role", "admin"));
});

builder.Services.AddSingleton<AuthConfig>(sp =>
{
    var c = new AuthConfig();
    c.AddAuthenticator(sp.GetRequiredService<TokenAuthenticator>());
    c.UseIssuer(sp.GetRequiredService<ITokenIssuer>());
    return c;
});
builder.Services.AddScoped<AuthService>();

// ─────────────────────────────────────────────────────────
// 2FA: SMS (sandbox — code printed to logs)
// ─────────────────────────────────────────────────────────
builder.Services.AddSmsTwoFactor();

builder.Services.AddSingleton<TwoFactorConfig>(sp =>
{
    var cfg = new TwoFactorConfig();
    foreach (var ch in sp.GetServices<ITwoFactorChannel>())
        cfg.AddChannel(ch);
    return cfg;
});
builder.Services.AddScoped<TwoFactorService>();

// ─────────────────────────────────────────────────────────
// Rate Limiting
// ─────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(
        httpContext => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 200,
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
// HSTS
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
    service = "Order.Admin.Api",
    description = "لوحة إدارة اوردر - مبنية على OperationEngine المحاسبي",
    version = "1.0.0",
    environment = app.Environment.EnvironmentName,
    docs = "/swagger"
}));

app.MapGet("/health", (IServiceProvider sp) => Results.Ok(new
{
    status = "healthy",
    time = DateTime.UtcNow,
    db = builder.Configuration["Database:Provider"] ?? "SQLite"
}));

// Create the DB schema then start
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider
        .GetRequiredService<ACommerce.SharedKernel.Infrastructure.EFCores.Context.ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();
    Log.Information("Database schema ready");

    // Seed a demo admin so Order.Admin login is testable end-to-end.
    var userRepo = scope.ServiceProvider
        .GetRequiredService<ACommerce.SharedKernel.Abstractions.Repositories.IRepositoryFactory>()
        .CreateRepository<Order.Api.Entities.User>();
    var existing = await userRepo.GetAllWithPredicateAsync(u => u.Role == "admin");
    if (!existing.Any())
    {
        await userRepo.AddAsync(new Order.Api.Entities.User
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            PhoneNumber = "+966599999999",
            FullName = "Demo Admin",
            Email = "admin@order.test",
            Role = "admin",
            IsActive = true
        }, default);
        Log.Information("Seeded demo admin (+966599999999)");
    }
}
catch (Exception ex)
{
    Log.Error(ex, "Database initialisation failed");
}

app.Run();
