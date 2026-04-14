using ACommerce.Authentication.Operations;
using ACommerce.Authentication.Operations.Abstractions;
using ACommerce.Authentication.Providers.Token;
using ACommerce.Authentication.Providers.Token.Extensions;
using ACommerce.Authentication.TwoFactor.Operations;
using ACommerce.Authentication.TwoFactor.Operations.Abstractions;
using ACommerce.Authentication.TwoFactor.Providers.Sms.Extensions;
using ACommerce.Culture.Interceptors;
using ACommerce.Notification.Operations;
using ACommerce.Notification.Operations.Abstractions;
using ACommerce.Notification.Providers.InApp.Extensions;
using ACommerce.OperationEngine.Core;
using ACommerce.Realtime.Operations.Abstractions;
using ACommerce.Realtime.Providers.InMemory.Extensions;
using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Infrastructure.EFCores.Extensions;
using Order.Api.Entities;
using Order.Api.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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
// Register Order entities for entity discovery
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
// Database (SQLite by default — file lives at ./data/order.db)
// ─────────────────────────────────────────────────────────
var dbProvider = builder.Configuration["Database:Provider"] ?? "SQLite";
var dbConnection = builder.Configuration["Database:ConnectionString"];

switch (dbProvider.ToLowerInvariant())
{
    case "sqlite":
        Directory.CreateDirectory("data");
        builder.Services.AddACommerceSQLite(dbConnection ?? "Data Source=data/order-platform.db");
        break;
    default:
        builder.Services.AddACommerceInMemoryDatabase("OrderPlatformDb");
        break;
}

// ─────────────────────────────────────────────────────────
// MVC + Swagger + CORS
// ─────────────────────────────────────────────────────────
// ─── Culture stack (numerals, datetime, phone, context middleware) ──
builder.Services.AddCultureStack();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:5701", "http://localhost:5801", "http://localhost:5201" };
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// ─────────────────────────────────────────────────────────
// OperationEngine (Scoped)
// ─────────────────────────────────────────────────────────
builder.Services.AddScoped<OpEngine>(sp =>
    new OpEngine(sp, sp.GetRequiredService<ILogger<OpEngine>>()));

// ─────────────────────────────────────────────────────────
// Realtime + Notifications (in-memory)
// ─────────────────────────────────────────────────────────
builder.Services.AddInMemoryRealtimeTransport();
builder.Services.AddInAppNotificationChannel(opt =>
{
    opt.MethodName = "ReceiveNotification";
    opt.AllowOffline = true;
});

// Register Notifier with domain notification types
builder.Services.AddNotifications(config =>
{
    config.DefineType(OrderNotifications.NewOrder);
    config.DefineType(OrderNotifications.OrderAccepted);
    config.DefineType(OrderNotifications.OrderReady);
    config.DefineType(OrderNotifications.OrderRejected);
    config.DefineType(OrderNotifications.OrderDelivered);
});
// InApp channel registered via AddInAppNotificationChannel above — Notifier resolves channels from DI at send time

// ─────────────────────────────────────────────────────────
// Authentication: JWT + SMS 2FA (mock)
// ─────────────────────────────────────────────────────────
var jwtOptions = new JwtOptions
{
    Issuer = builder.Configuration["JWT:Issuer"] ?? "https://order.app",
    Audience = builder.Configuration["JWT:Audience"] ?? "order-api",
    SecretKey = builder.Configuration["JWT:SecretKey"]
        ?? "Order-dev-secret-do-not-use-in-prod-32chars-min!!!",
    AccessTokenLifetime = TimeSpan.TryParse(
        builder.Configuration["JWT:AccessTokenLifetime"], out var lt) ? lt : TimeSpan.FromDays(30)
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

// 2FA: SMS (sandbox — code printed to logs)
builder.Services.AddSmsTwoFactor();
builder.Services.AddSingleton<TwoFactorConfig>(sp =>
{
    var cfg = new TwoFactorConfig();
    foreach (var ch in sp.GetServices<ITwoFactorChannel>())
        cfg.AddChannel(ch);
    return cfg;
});
builder.Services.AddScoped<TwoFactorService>();

// ─── HTTP Client → Vendor.Api (for order webhooks) ──────────────────
var vendorApiBase = builder.Configuration["VendorApi:BaseUrl"] ?? "http://localhost:5201";
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

// Seeder
builder.Services.AddScoped<OrderSeeder>();

// ─────────────────────────────────────────────────────────
// Build
// ─────────────────────────────────────────────────────────
var app = builder.Build();

// Bridge DI-registered notification channels into NotificationConfig
using (var chScope = app.Services.CreateScope())
{
    var notifConfig = chScope.ServiceProvider.GetRequiredService<NotificationConfig>();
    foreach (var ch in chScope.ServiceProvider.GetServices<INotificationChannel>())
        notifConfig.AddChannel(ch);
}

app.UseCors();
// Populate ICultureContext per-request from X-Timezone / Accept-Language.
app.UseCultureContext();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "Order.Api",
    description = "خدمة اوردر الخلفية المبنية على OperationEngine المحاسبي + SQLite",
    version = "1.0.0",
    docs = "/swagger"
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    time = DateTime.UtcNow,
    db = builder.Configuration["Database:Provider"] ?? "SQLite"
}));

// Create the DB schema (SQLite — picks up every entity registered above)
// then seed demo data.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider
        .GetRequiredService<ACommerce.SharedKernel.Infrastructure.EFCores.Context.ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();
    Log.Information("Database schema ready");

    var seeder = scope.ServiceProvider.GetRequiredService<OrderSeeder>();
    await seeder.SeedAsync();
    Log.Information("Seeding complete");
}
catch (Exception ex)
{
    Log.Error(ex, "Database initialisation failed");
}

app.Run();
